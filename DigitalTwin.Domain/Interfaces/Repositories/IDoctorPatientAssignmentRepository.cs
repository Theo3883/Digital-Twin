using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Repositories;

public interface IDoctorPatientAssignmentRepository
{
    /// <summary>Get all patient IDs assigned to a specific doctor.</summary>
    Task<IEnumerable<DoctorPatientAssignment>> GetByDoctorIdAsync(Guid doctorId);

    /// <summary>Get all doctors assigned to a specific patient.</summary>
    Task<IEnumerable<DoctorPatientAssignment>> GetByPatientIdAsync(Guid patientId);

    /// <summary>Get all doctors assigned to a patient email.</summary>
    Task<IEnumerable<DoctorPatientAssignment>> GetByPatientEmailAsync(string patientEmail);

    /// <summary>Get a specific assignment.</summary>
    Task<DoctorPatientAssignment?> GetByDoctorAndPatientAsync(Guid doctorId, Guid patientId);

    /// <summary>Check if a doctor is assigned to a patient.</summary>
    Task<bool> IsAssignedAsync(Guid doctorId, Guid patientId);

    /// <summary>Assign a patient to a doctor by patient email.</summary>
    Task AddAsync(DoctorPatientAssignment assignment);

    /// <summary>Remove an assignment.</summary>
    Task RemoveAsync(Guid doctorId, Guid patientId);

    // ── Sync helpers (legacy offline sync; unused by cloud-only Web API) ─────

    /// <summary>Get locally-created assignments that have not been pushed to the cloud yet.</summary>
    Task<IEnumerable<DoctorPatientAssignment>> GetDirtyAsync();

    /// <summary>Mark a set of assignments as synced (clears IsDirty, sets SyncedAt).</summary>
    Task MarkSyncedAsync(IEnumerable<DoctorPatientAssignment> assignments);

    /// <summary>
    /// Upsert a batch of assignments pulled from the cloud into a local store (offline clients).
    /// Reactivates soft-deleted rows and soft-deletes rows no longer present in the cloud set.
    /// </summary>
    Task UpsertRangeFromCloudAsync(Guid patientId, IEnumerable<DoctorPatientAssignment> cloudAssignments);
}
