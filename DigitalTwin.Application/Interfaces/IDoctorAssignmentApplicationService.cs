using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Defines operations for retrieving doctor assignments for a patient.
/// </summary>
public interface IDoctorAssignmentApplicationService
{
    /// <summary>
    /// Gets the doctors assigned to the patient identified by email.
    /// </summary>
    Task<IEnumerable<AssignedDoctorDto>> GetAssignedDoctorsAsync(string patientEmail);
}

