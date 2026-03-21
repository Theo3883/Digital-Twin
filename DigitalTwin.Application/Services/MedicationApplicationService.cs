using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Mappers;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Events;
using DigitalTwin.Domain.Exceptions;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Orchestrates patient-side medication workflows, interaction checks, and DTO mapping.
/// All business rules live in domain services; this class is a pure coordinator.
/// </summary>
public class MedicationApplicationService : IMedicationApplicationService
{
    private readonly IMedicationInteractionProvider  _interactionProvider;
    private readonly IDrugSearchProvider             _drugSearch;
    private readonly IRxCuiLookupProvider            _rxCuiLookup;
    private readonly IMedicationInteractionService   _interactionService;
    private readonly IMedicationService              _medicationService;
    private readonly IMedicationManagementService    _medications;
    private readonly IDomainEventDispatcher          _events;
    private readonly IHealthDataSyncService?         _sync;
    private readonly ILogger<MedicationApplicationService>? _logger;

    public MedicationApplicationService(
        IMedicationInteractionProvider interactionProvider,
        IDrugSearchProvider drugSearch,
        IRxCuiLookupProvider rxCuiLookup,
        IMedicationInteractionService interactionService,
        IMedicationService medicationService,
        IMedicationManagementService medications,
        IDomainEventDispatcher events,
        IHealthDataSyncService? sync = null,
        ILogger<MedicationApplicationService>? logger = null)
    {
        _interactionProvider = interactionProvider;
        _drugSearch          = drugSearch;
        _rxCuiLookup         = rxCuiLookup;
        _interactionService  = interactionService;
        _medicationService   = medicationService;
        _medications         = medications;
        _events              = events;
        _sync                = sync;
        _logger              = logger;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<MedicationInteractionDto>> CheckInteractionsAsync(
        IEnumerable<string> rxCuis)
    {
        var interactions = await _interactionProvider.GetInteractionsAsync(rxCuis);
        return interactions.Select(MedicationInteractionMapper.ToDto);
    }

    /// <inheritdoc/>
    public async Task<bool> HasHighRiskInteractionsAsync(IEnumerable<string> rxCuis)
    {
        var interactions = await _interactionProvider.GetInteractionsAsync(rxCuis);
        return _interactionService.HasHighRisk(interactions);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<DrugSearchResultDto>> SearchDrugsByNameAsync(
        string query, int maxResults = 8, CancellationToken ct = default)
    {
        var results = await _drugSearch.SearchByNameAsync(query, maxResults, ct);
        return results.Select(r => new DrugSearchResultDto(r.Name, r.RxCui));
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<MedicationDto>> GetMyMedicationsAsync(Guid patientId)
    {
        // Pull cloud → local first so doctor-prescribed medications are always visible
        // without requiring a manual refresh. The gate inside PushToCloudAsync prevents
        // concurrent runs.
        if (_sync is not null)
        {
            try { await _sync.PushToCloudAsync(); }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[Medications] Background cloud pull failed — returning local data.");
            }
        }

        var medications = await _medications.GetByPatientAsync(patientId);
        return medications.Select(ToDto);
    }

    /// <summary>
    /// Adds a medication for the specified patient after resolving its RxCUI and
    /// verifying it does not produce a high-risk interaction with any existing
    /// active medication.
    /// </summary>
    /// <exception cref="MedicationInteractionBlockedException">
    /// Thrown when the new medication creates a High-severity interaction with one or
    /// more of the patient's current active medications.
    /// </exception>
    public async Task<MedicationDto> AddMedicationAsync(
        Guid patientId, AddMedicationDto dto, AddedByRole addedBy)
    {
        // 1. Resolve RxCUI — prefer the value already picked from autocomplete,
        //    otherwise run the chained lookup (RxNav approximateTerm → Gemini fallback).
        var rxCui = dto.RxCui;
        if (string.IsNullOrWhiteSpace(rxCui) && !string.IsNullOrWhiteSpace(dto.Name))
            rxCui = await _rxCuiLookup.LookupRxCuiAsync(dto.Name.Trim());

        // 2. Interaction safety check — only possible when we have an RxCUI.
        if (!string.IsNullOrWhiteSpace(rxCui))
            await EnforceInteractionSafetyAsync(patientId, rxCui);

        // 3. Build and persist the medication.
        var medication = _medicationService.CreateMedication(new Domain.Models.CreateMedicationRequest(
            PatientId:          patientId,
            Name:               dto.Name,
            Dosage:             dto.Dosage,
            Frequency:          dto.Frequency,
            Route:              dto.Route,
            RxCui:              rxCui,
            Instructions:       dto.Instructions,
            Reason:             dto.Reason,
            PrescribedByUserId: null,
            StartDate:          dto.StartDate,
            AddedByRole:        addedBy));

        await _medications.AddAsync(medication);

        await _events.DispatchAsync(
            new MedicationAddedEvent(patientId, medication.Id, medication.Name));

        return ToDto(medication);
    }

    /// <inheritdoc/>
    public async Task DeleteMedicationAsync(Guid patientId, Guid medicationId)
    {
        await _medications.SoftDeleteAsync(patientId, medicationId);
        await _events.DispatchAsync(new MedicationDeletedEvent(patientId, medicationId));
    }

    /// <inheritdoc/>
    public async Task DiscontinueMedicationAsync(
        Guid patientId, Guid medicationId, string? reason = null)
    {
        await _medications.DiscontinueAsync(patientId, medicationId, reason);
        await _events.DispatchAsync(
            new MedicationDiscontinuedEvent(patientId, medicationId, reason));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Fetches the patient's active medications that have an RxCUI, checks interactions
    /// between the candidate CUI and every existing CUI in a single batch call, then
    /// throws <see cref="MedicationInteractionBlockedException"/> if any blocking
    /// interactions are found.
    /// </summary>
    private async Task EnforceInteractionSafetyAsync(Guid patientId, string newRxCui)
    {
        var existing = await _medications.GetByPatientAsync(patientId);

        var existingCuis = existing
            .Where(m => m.Status == MedicationStatus.Active
                     && !string.IsNullOrWhiteSpace(m.RxCui))
            .Select(m => m.RxCui!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (existingCuis.Count == 0)
            return;

        // Build full list: new drug + all existing active ingredient CUIs.
        var allCuis = existingCuis
            .Append(newRxCui)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allInteractions = await _interactionProvider.GetInteractionsAsync(allCuis);

        // Keep only interactions that involve the new drug — we don't want to block
        // addition because of pre-existing interactions between already-approved meds.
        var newDrugInteractions = allInteractions
            .Where(i => i.DrugARxCui.Equals(newRxCui, StringComparison.OrdinalIgnoreCase)
                     || i.DrugBRxCui.Equals(newRxCui, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var blocking = _interactionService
            .GetBlockingInteractions(newDrugInteractions)
            .ToList();

        if (blocking.Count > 0)
            throw new MedicationInteractionBlockedException(blocking);
    }

    private static MedicationDto ToDto(Domain.Models.Medication m) => new()
    {
        Id                 = m.Id,
        Name               = m.Name,
        Dosage             = m.Dosage,
        Frequency          = m.Frequency,
        Route              = m.Route,
        Status             = m.Status,
        RxCui              = m.RxCui,
        Instructions       = m.Instructions,
        Reason             = m.Reason,
        PrescribedByUserId = m.PrescribedByUserId,
        StartDate          = m.StartDate,
        EndDate            = m.EndDate,
        DiscontinuedReason = m.DiscontinuedReason,
        AddedByRole        = m.AddedByRole,
        CreatedAt          = m.CreatedAt
    };
}
