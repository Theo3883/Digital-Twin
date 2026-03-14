using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Mappers;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Services;

namespace DigitalTwin.Application.Services;

public class MedicationApplicationService : IMedicationApplicationService
{
    private readonly IMedicationInteractionProvider _provider;
    private readonly IDrugSearchProvider _drugSearch;
    private readonly IRxCuiLookupProvider _rxCuiLookup;
    private readonly IMedicationInteractionService _interactionService;
    private readonly IMedicationService _medicationService;
    private readonly IMedicationRepository _medications;

    public MedicationApplicationService(
        IMedicationInteractionProvider provider,
        IDrugSearchProvider drugSearch,
        IRxCuiLookupProvider rxCuiLookup,
        IMedicationInteractionService interactionService,
        IMedicationService medicationService,
        IMedicationRepository medications)
    {
        _provider = provider;
        _drugSearch = drugSearch;
        _rxCuiLookup = rxCuiLookup;
        _interactionService = interactionService;
        _medicationService = medicationService;
        _medications = medications;
    }

    // ── Drug interaction checking ─────────────────────────────────────────────

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

    // ── Drug name / RxCUI autocomplete ────────────────────────────────────────

    public async Task<IEnumerable<DrugSearchResultDto>> SearchDrugsByNameAsync(
        string query, int maxResults = 8, CancellationToken ct = default)
    {
        var results = await _drugSearch.SearchByNameAsync(query, maxResults, ct);
        return results.Select(r => new DrugSearchResultDto(r.Name, r.RxCui));
    }

    // ── CRUD (patient-side, orchestration only) ───────────────────────────────

    public async Task<IEnumerable<MedicationDto>> GetMyMedicationsAsync(Guid patientId)
    {
        var medications = await _medications.GetByPatientAsync(patientId);
        return medications.Select(ToDto);
    }

    public async Task<MedicationDto> AddMedicationAsync(
        Guid patientId, AddMedicationDto dto, AddedByRole addedBy)
    {
        var rxCui = dto.RxCui;

        // Fallback: if no RxCUI was selected from autocomplete, use Gemini (low temp) to resolve from name
        if (string.IsNullOrWhiteSpace(rxCui) && !string.IsNullOrWhiteSpace(dto.Name))
            rxCui = await _rxCuiLookup.LookupRxCuiAsync(dto.Name.Trim());

        var medication = _medicationService.CreateMedication(
            patientId,
            dto.Name,
            dto.Dosage,
            dto.Frequency,
            dto.Route,
            rxCui,
            dto.Instructions,
            dto.Reason,
            prescribedByUserId: null,
            dto.StartDate,
            addedBy);

        await _medications.AddAsync(medication);
        return ToDto(medication);
    }

    public async Task DeleteMedicationAsync(Guid patientId, Guid medicationId)
    {
        var existing = await _medications.GetByIdAsync(medicationId);
        if (existing is null || existing.PatientId != patientId)
            throw new InvalidOperationException("Medication not found or does not belong to this patient.");
        await _medications.SoftDeleteAsync(medicationId);
    }

    public async Task DiscontinueMedicationAsync(Guid patientId, Guid medicationId, string? reason = null)
    {
        var existing = await _medications.GetByIdAsync(medicationId);
        if (existing is null || existing.PatientId != patientId)
            throw new InvalidOperationException("Medication not found or does not belong to this patient.");
        var endDate = DateTime.UtcNow;
        await _medications.DiscontinueAsync(medicationId, endDate, reason);
    }

    private static MedicationDto ToDto(Domain.Models.Medication m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Dosage = m.Dosage,
        Frequency = m.Frequency,
        Route = m.Route,
        Status = m.Status,
        RxCui = m.RxCui,
        Instructions = m.Instructions,
        Reason = m.Reason,
        PrescribedByUserId = m.PrescribedByUserId,
        StartDate = m.StartDate,
        EndDate = m.EndDate,
        DiscontinuedReason = m.DiscontinuedReason,
        AddedByRole = m.AddedByRole,
        CreatedAt = m.CreatedAt
    };
}
