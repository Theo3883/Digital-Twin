using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

public interface IDoctorAssignmentApplicationService
{
    Task<IEnumerable<AssignedDoctorDto>> GetAssignedDoctorsAsync(string patientEmail);
}

