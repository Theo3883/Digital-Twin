using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Repositories;

public interface IDoctorPatientAssignmentRepository
{
    /// <summary>Get all patient IDs assigned to a specific doctor.</summary>
    Task<IEnumerable<DoctorPatientAssignment>> GetByDoctorIdAsync(long doctorId);

    /// <summary>Get a specific assignment.</summary>
    Task<DoctorPatientAssignment?> GetByDoctorAndPatientAsync(long doctorId, long patientId);

    /// <summary>Check if a doctor is assigned to a patient.</summary>
    Task<bool> IsAssignedAsync(long doctorId, long patientId);

    /// <summary>Assign a patient to a doctor by patient email.</summary>
    Task AddAsync(DoctorPatientAssignment assignment);

    /// <summary>Remove an assignment.</summary>
    Task RemoveAsync(long doctorId, long patientId);
}
