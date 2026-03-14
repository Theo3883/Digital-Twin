using DigitalTwin.Application.DTOs;
using DigitalTwin.Domain.Enums;

namespace DigitalTwin.Application.Interfaces;

public interface IMedicationApplicationService
{
    // ── Drug interaction checking (RxNav) ────────────────────────────────────
    Task<IEnumerable<MedicationInteractionDto>> CheckInteractionsAsync(IEnumerable<string> rxCuis);
    Task<bool> HasHighRiskInteractionsAsync(IEnumerable<string> rxCuis);

    // ── Drug name / RxCUI autocomplete ───────────────────────────────────────
    Task<IEnumerable<DrugSearchResultDto>> SearchDrugsByNameAsync(
        string query, int maxResults = 8, CancellationToken ct = default);

    // ── CRUD (patient-side) ──────────────────────────────────────────────────
    Task<IEnumerable<MedicationDto>> GetMyMedicationsAsync(Guid patientId);
    Task<MedicationDto> AddMedicationAsync(Guid patientId, AddMedicationDto dto, AddedByRole addedBy);
    Task DeleteMedicationAsync(Guid patientId, Guid medicationId);
    Task DiscontinueMedicationAsync(Guid patientId, Guid medicationId, string? reason = null);
}
