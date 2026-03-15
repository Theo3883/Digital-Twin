using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Mappers;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Events;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Interfaces.Services;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Thin orchestrator for patient-side medication operations.
/// Business rules (ownership, creation) live in <see cref="IMedicationService"/>.
/// Persistence strategy (cloud-first / local-fallback) lives in <see cref="IMedicationManagementService"/>.
/// This class only: validates/maps inputs, calls domain, dispatches events, maps to DTOs.
/// No repository interfaces are injected here.
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

    public MedicationApplicationService(
        IMedicationInteractionProvider provider,
        IDrugSearchProvider drugSearch,
        IRxCuiLookupProvider rxCuiLookup,
        IMedicationInteractionService interactionService,
        IMedicationService medicationService,
        IMedicationManagementService medications,
        IDomainEventDispatcher events)
    {
        _provider           = provider;
        _drugSearch         = drugSearch;
        _rxCuiLookup        = rxCuiLookup;
        _interactionService = interactionService;
        _medicationService  = medicationService;
        _medications        = medications;
        _events             = events;
    }

    // ── Drug interaction checking ─────────────────────────────────────────

    public async Task<IEnumerable<MedicationInteractionDto>> CheckInteractionsAsync(
        IEnumerable<string> rxCuis)
    {
        var interactions = await _provider.GetInteractionsAsync(rxCuis);
        return interactions.Select(MedicationInteractionMapper.ToDto);
    }

    public async Task<bool> HasHighRiskInteractionsAsync(IEnumerable<string> rxCuis)
    {
        var interactions = await _provider.GetInteractionsAsync(rxCuis);
        return _interactionService.HasHighRisk(interactions);
    }

    // ── Drug name / RxCUI autocomplete ────────────────────────────────────

    public async Task<IEnumerable<DrugSearchResultDto>> SearchDrugsByNameAsync(
        string query, int maxResults = 8, CancellationToken ct = default)
    {
        var results = await _drugSearch.SearchByNameAsync(query, maxResults, ct);
        return results.Select(r => new DrugSearchResultDto(r.Name, r.RxCui));
    }

    // ── CRUD (patient-side, orchestration only) ───────────────────────────

    public async Task<IEnumerable<MedicationDto>> GetMyMedicationsAsync(Guid patientId)
    {
        var medications = await _medications.GetByPatientAsync(patientId);
        return medications.Select(ToDto);
    }

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

    public async Task DeleteMedicationAsync(Guid patientId, Guid medicationId)
    {
        await _medications.SoftDeleteAsync(patientId, medicationId);
        await _events.DispatchAsync(new MedicationDeletedEvent(patientId, medicationId));
    }

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
