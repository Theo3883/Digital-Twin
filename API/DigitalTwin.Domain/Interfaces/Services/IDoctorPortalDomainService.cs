using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Services;

/// <summary>
/// Domain service for all doctor-portal business operations.
/// Owns: doctor-patient authorization, assignment factory, data retrieval
/// guarded by assignment scope. All methods enforce the invariant that a
/// doctor can only operate on explicitly assigned patients.
/// Repository interfaces are injected here; the application layer is a thin
/// orchestrator that calls this service and maps results to DTOs.
/// </summary>
public interface IDoctorPortalDomainService
{
    // ── Authorization ────────────────────────────────────────────────────────

    /// <summary>Resolves doctor by email. Throws <see cref="Exceptions.NotFoundException"/> if not found.</summary>
    Task<User> GetDoctorByEmailAsync(string doctorEmail, CancellationToken ct = default);

    /// <summary>
    /// Returns the doctor's Guid after verifying they are assigned to <paramref name="patientId"/>.
    /// Throws <see cref="Exceptions.UnauthorizedException"/> if not assigned.
    /// </summary>
    Task<Guid> RequireAuthorizedDoctorIdAsync(string doctorEmail, Guid patientId, CancellationToken ct = default);

    // ── Patient lookup ───────────────────────────────────────────────────────

    Task<IEnumerable<DoctorPatientAssignment>> GetAssignmentsForDoctorAsync(Guid doctorId, CancellationToken ct = default);
    Task<(User user, Patient patient)> GetPatientWithUserAsync(Guid patientId, CancellationToken ct = default);

    // ── Assignment factory (Guard + Factory patterns) ────────────────────────

    /// <summary>
    /// Creates and persists a doctor-patient assignment.
    /// Guards: doctor exists; patient exists; not already assigned.
    /// Dispatches <see cref="Events.PatientAssignedEvent"/>.
    /// Throws <see cref="Exceptions.DuplicateAssignmentException"/> when already assigned.
    /// </summary>
    Task<DoctorPatientAssignment> AssignPatientAsync(
        string doctorEmail, string patientEmail, string? notes, CancellationToken ct = default);

    /// <summary>
    /// Removes a doctor-patient assignment.
    /// Dispatches <see cref="Events.PatientUnassignedEvent"/>.
    /// </summary>
    Task UnassignPatientAsync(string doctorEmail, Guid patientId, CancellationToken ct = default);

    // ── Scoped data reads ────────────────────────────────────────────────────

    Task<IReadOnlyList<VitalSign>> GetPatientVitalsAsync(
        Guid patientId, VitalSignType? type, DateTime? from, DateTime? to, CancellationToken ct = default);

    Task<IReadOnlyList<SleepSession>> GetPatientSleepAsync(
        Guid patientId, DateTime? from, DateTime? to, CancellationToken ct = default);

    Task<IReadOnlyList<Medication>> GetPatientMedicationsAsync(
        Guid patientId, CancellationToken ct = default);

    // ── Medication management ────────────────────────────────────────────────

    Task<Medication> AddPatientMedicationAsync(
        Guid patientId, CreateMedicationRequest request, CancellationToken ct = default);

    Task SoftDeleteMedicationAsync(
        Guid patientId, Guid medicationId, CancellationToken ct = default);

    Task DiscontinueMedicationAsync(
        Guid patientId, Guid medicationId, string reason, CancellationToken ct = default);
}
