using DigitalTwin.Application.DTOs;
using DigitalTwin.Domain.Enums;

namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Defines patient medication operations exposed by the application layer.
/// </summary>
public interface IMedicationApplicationService
{
    /// <summary>
    /// Checks interaction details for the supplied RxCUI identifiers.
    /// </summary>
    Task<IEnumerable<MedicationInteractionDto>> CheckInteractionsAsync(IEnumerable<string> rxCuis);

    /// <summary>
    /// Determines whether the supplied medications include any high-risk interactions.
    /// </summary>
    Task<bool> HasHighRiskInteractionsAsync(IEnumerable<string> rxCuis);

    /// <summary>
    /// Searches medications by name and returns matching RxCUI candidates.
    /// </summary>
    Task<IEnumerable<DrugSearchResultDto>> SearchDrugsByNameAsync(
        string query, int maxResults = 8, CancellationToken ct = default);

    /// <summary>
    /// Gets medications and auto-interaction data. Uses preferences cache when valid (MAUI);
    /// skips cloud sync until TTL expires unless <paramref name="forceRefresh"/> is true.
    /// </summary>
    Task<MedicationListCache> GetMyMedicationsAsync(Guid patientId, bool forceRefresh = false);

    /// <summary>
    /// Adds a medication for the specified patient.
    /// </summary>
    Task<MedicationDto> AddMedicationAsync(Guid patientId, AddMedicationDto dto, AddedByRole addedBy, bool skipInteractionCheck = false);

    /// <summary>
    /// Soft-deletes a medication for the specified patient.
    /// </summary>
    Task DeleteMedicationAsync(Guid patientId, Guid medicationId);

    /// <summary>
    /// Discontinues a medication for the specified patient.
    /// </summary>
    Task DiscontinueMedicationAsync(Guid patientId, Guid medicationId, string? reason = null);
}
