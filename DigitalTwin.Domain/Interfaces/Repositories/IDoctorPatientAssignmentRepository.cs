using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Repositories;

public interface IDoctorPatientAssignmentRepository
{
    /// <summary>Get all patient IDs assigned to a specific doctor.</summary>
    Task<IEnumerable<DoctorPatientAssignment>> GetByDoctorIdAsync(Guid doctorId);

    /// <summary>Get a specific assignment.</summary>
    Task<DoctorPatientAssignment?> GetByDoctorAndPatientAsync(Guid doctorId, Guid patientId);

    /// <summary>Check if a doctor is assigned to a patient.</summary>
    Task<bool> IsAssignedAsync(Guid doctorId, Guid patientId);

    /// <summary>Assign a patient to a doctor by patient email.</summary>
    Task AddAsync(DoctorPatientAssignment assignment);

    /// <summary>Remove an assignment.</summary>
    Task RemoveAsync(Guid doctorId, Guid patientId);
}
