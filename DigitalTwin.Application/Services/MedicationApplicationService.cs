using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Mappers;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Events;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Orchestrates patient-side medication workflows, interaction checks, and DTO mapping.
/// </summary>
public class MedicationApplicationService : IMedicationApplicationService
{
    private readonly IMedicationInteractionProvider  _provider;
    private readonly IDrugSearchProvider             _drugSearch;
    private readonly IRxCuiLookupProvider            _rxCuiLookup;
    private readonly IMedicationInteractionService   _interactionService;
    private readonly IMedicationService              _medicationService;
    private readonly IMedicationManagementService    _medications;
    private readonly IDomainEventDispatcher          _events;
    private readonly IHealthDataSyncService?         _sync;
    private readonly ILogger<MedicationApplicationService>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MedicationApplicationService"/> class.
    /// </summary>
    public MedicationApplicationService(
        IMedicationInteractionProvider provider,
        IDrugSearchProvider drugSearch,
        IRxCuiLookupProvider rxCuiLookup,
        IMedicationInteractionService interactionService,
        IMedicationService medicationService,
        IMedicationManagementService medications,
        IDomainEventDispatcher events,
        IHealthDataSyncService? sync = null,
        ILogger<MedicationApplicationService>? logger = null)
    {
        _provider           = provider;
        _drugSearch         = drugSearch;
        _rxCuiLookup        = rxCuiLookup;
        _interactionService = interactionService;
        _medicationService  = medicationService;
        _medications        = medications;
        _events             = events;
        _sync               = sync;
        _logger             = logger;
    }

    /// <summary>
    /// Retrieves medication interaction details for the supplied RxCUI identifiers.
    /// </summary>
    public async Task<IEnumerable<MedicationInteractionDto>> CheckInteractionsAsync(
        IEnumerable<string> rxCuis)
    {
        var interactions = await _provider.GetInteractionsAsync(rxCuis);
        return interactions.Select(MedicationInteractionMapper.ToDto);
    }

    /// <summary>
    /// Determines whether the supplied medications include a high-risk interaction.
    /// </summary>
    public async Task<bool> HasHighRiskInteractionsAsync(IEnumerable<string> rxCuis)
    {
        var interactions = await _provider.GetInteractionsAsync(rxCuis);
        return _interactionService.HasHighRisk(interactions);
    }

    /// <summary>
    /// Searches medications by name and returns matching RxCUI results.
    /// </summary>
    public async Task<IEnumerable<DrugSearchResultDto>> SearchDrugsByNameAsync(
        string query, int maxResults = 8, CancellationToken ct = default)
    {
        var results = await _drugSearch.SearchByNameAsync(query, maxResults, ct);
        return results.Select(r => new DrugSearchResultDto(r.Name, r.RxCui));
    }

    /// <summary>
    /// Gets medications for the specified patient, pulling the latest from cloud first
    /// so doctor-prescribed medications are always visible without manual refresh.
    /// </summary>
    public async Task<IEnumerable<MedicationDto>> GetMyMedicationsAsync(Guid patientId)
    {
        // Pull cloud → local before returning so any medication a doctor has just
        // prescribed appears immediately. The gate inside PushToCloudAsync prevents
        // concurrent runs, so this is safe to call on every page load.
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
    /// Adds a medication for the specified patient and dispatches the corresponding domain event.
    /// </summary>
    public async Task<MedicationDto> AddMedicationAsync(
        Guid patientId, AddMedicationDto dto, AddedByRole addedBy)
    {
        // Fallback: if no RxCUI was selected from autocomplete, resolve from name.
        var rxCui = dto.RxCui;
        if (string.IsNullOrWhiteSpace(rxCui) && !string.IsNullOrWhiteSpace(dto.Name))
            rxCui = await _rxCuiLookup.LookupRxCuiAsync(dto.Name.Trim());

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

    /// <summary>
    /// Soft-deletes a medication and dispatches a deletion event.
    /// </summary>
    public async Task DeleteMedicationAsync(Guid patientId, Guid medicationId)
    {
        await _medications.SoftDeleteAsync(patientId, medicationId);
        await _events.DispatchAsync(new MedicationDeletedEvent(patientId, medicationId));
    }

    /// <summary>
    /// Discontinues a medication and dispatches a discontinuation event.
    /// </summary>
    public async Task DiscontinueMedicationAsync(
        Guid patientId, Guid medicationId, string? reason = null)
    {
        await _medications.DiscontinueAsync(patientId, medicationId, reason);
        await _events.DispatchAsync(
            new MedicationDiscontinuedEvent(patientId, medicationId, reason));
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
