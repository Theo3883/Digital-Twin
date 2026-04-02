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
    private readonly IPreferencesJsonCache?            _prefs;
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
        IPreferencesJsonCache? prefs = null,
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
        _prefs               = prefs;
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
    public async Task<MedicationListCache> GetMyMedicationsAsync(Guid patientId, bool forceRefresh = false)
    {
        if (!forceRefresh && _prefs is not null)
        {
            var cached = TryGetValidCache(patientId);
            if (cached is not null)
                return cached;
        }

        return await LoadFullAndPersistAsync(patientId).ConfigureAwait(false);
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
        Guid patientId, AddMedicationDto dto, AddedByRole addedBy, bool skipInteractionCheck = false)
    {
        var rxCui = dto.RxCui;
        if (string.IsNullOrWhiteSpace(rxCui) && !string.IsNullOrWhiteSpace(dto.Name))
            rxCui = await _rxCuiLookup.LookupRxCuiAsync(dto.Name.Trim());

        if (!skipInteractionCheck && !string.IsNullOrWhiteSpace(rxCui))
            await EnforceInteractionSafetyAsync(patientId, rxCui);

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

        await RebuildMedicationCacheFromLocalAsync(patientId).ConfigureAwait(false);

        return ToDto(medication);
    }

    /// <inheritdoc/>
    public async Task DeleteMedicationAsync(Guid patientId, Guid medicationId)
    {
        await _medications.SoftDeleteAsync(patientId, medicationId);
        await _events.DispatchAsync(new MedicationDeletedEvent(patientId, medicationId));
        await RebuildMedicationCacheFromLocalAsync(patientId).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DiscontinueMedicationAsync(
        Guid patientId, Guid medicationId, string? reason = null)
    {
        await _medications.DiscontinueAsync(patientId, medicationId, reason);
        await _events.DispatchAsync(
            new MedicationDiscontinuedEvent(patientId, medicationId, reason));
        await RebuildMedicationCacheFromLocalAsync(patientId).ConfigureAwait(false);
    }

    // ── Cache helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Skipping PushToCloud on cache-valid reads can delay doctor-prescribed updates from cloud until TTL or force refresh.
    /// </summary>
    private MedicationListCache? TryGetValidCache(Guid patientId)
    {
        if (_prefs is null)
            return null;

        var cached = _prefs.Get<MedicationListCache>(MedicationListCachePreferences.Key);
        if (cached is null)
            return null;
        if (cached.PatientId != patientId)
            return null;
        if (DateTime.UtcNow - cached.CachedAtUtc > MedicationListCachePreferences.Ttl)
            return null;

        return cached;
    }

    private async Task<MedicationListCache> LoadFullAndPersistAsync(Guid patientId)
    {
        if (_sync is not null)
        {
            try { await _sync.PushToCloudAsync().ConfigureAwait(false); }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[Medications] Background cloud pull failed — using local data for cache.");
            }
        }

        var medications = await _medications.GetByPatientAsync(patientId).ConfigureAwait(false);
        var medDtos = medications.Select(ToDto).ToList();
        var auto = await ComputeAutoInteractionsAsync(medDtos).ConfigureAwait(false);

        var snapshot = new MedicationListCache
        {
            PatientId = patientId,
            CachedAtUtc = DateTime.UtcNow,
            Medications = medDtos,
            AutoInteractions = auto
        };

        _prefs?.Set(MedicationListCachePreferences.Key, snapshot);
        return snapshot;
    }

    private async Task RebuildMedicationCacheFromLocalAsync(Guid patientId)
    {
        if (_prefs is null)
            return;

        var medications = await _medications.GetByPatientAsync(patientId).ConfigureAwait(false);
        var medDtos = medications.Select(ToDto).ToList();
        var auto = await ComputeAutoInteractionsAsync(medDtos).ConfigureAwait(false);

        _prefs.Set(MedicationListCachePreferences.Key, new MedicationListCache
        {
            PatientId = patientId,
            CachedAtUtc = DateTime.UtcNow,
            Medications = medDtos,
            AutoInteractions = auto
        });
    }

    private async Task<List<MedicationInteractionDto>> ComputeAutoInteractionsAsync(
        List<MedicationDto> medDtos)
    {
        var rxCuis = medDtos
            .Where(m => !string.IsNullOrWhiteSpace(m.RxCui))
            .Select(m => m.RxCui!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rxCuis.Count < 2)
            return [];

        try
        {
            var interactions = await _interactionProvider.GetInteractionsAsync(rxCuis).ConfigureAwait(false);
            return interactions.Select(MedicationInteractionMapper.ToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "[Medications] Auto interaction check failed.");
            return [];
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

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

        var allCuis = existingCuis
            .Append(newRxCui)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allInteractions = await _interactionProvider.GetInteractionsAsync(allCuis);

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
