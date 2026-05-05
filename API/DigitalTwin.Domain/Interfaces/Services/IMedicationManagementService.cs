using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Services;

/// <summary>
/// Domain service for patient-side medication management.
/// Owns: cloud-first persistence strategy, ownership guards, and retrieval.
/// The application layer is a pure orchestrator that calls this service
/// and maps results to DTOs.
/// </summary>
public interface IMedicationManagementService
{
    Task<IEnumerable<Medication>> GetByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<Medication?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Persists a new medication using cloud-first / local-fallback strategy.</summary>
    Task AddAsync(Medication medication, CancellationToken ct = default);

    /// <summary>
    /// Guards ownership then soft-deletes the medication.
    /// Throws <see cref="Exceptions.MedicationOwnershipException"/> if the medication does not
    /// belong to <paramref name="patientId"/>.
    /// </summary>
    Task SoftDeleteAsync(Guid patientId, Guid medicationId, CancellationToken ct = default);

    /// <summary>
    /// Guards ownership then discontinues the medication.
    /// Throws <see cref="Exceptions.MedicationOwnershipException"/> if the medication does not
    /// belong to <paramref name="patientId"/>.
    /// </summary>
    Task DiscontinueAsync(Guid patientId, Guid medicationId, string? reason, CancellationToken ct = default);
}
