using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Services;

public interface IDoctorPatientAssignmentService
{
    Task<IEnumerable<AssignedDoctorInfo>> GetAssignedDoctorsAsync(string patientEmail);
}

