using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Interfaces.Services;

namespace DigitalTwin.Application.Services;

public class DoctorAssignmentApplicationService : IDoctorAssignmentApplicationService
{
    private readonly IDoctorPatientAssignmentService _assignmentService;

    public DoctorAssignmentApplicationService(IDoctorPatientAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    public async Task<IEnumerable<AssignedDoctorDto>> GetAssignedDoctorsAsync(string patientEmail)
    {
        var assigned = await _assignmentService.GetAssignedDoctorsAsync(patientEmail);
        return assigned.Select(a => new AssignedDoctorDto
        {
            DoctorId = a.Doctor.Id,
            FullName = $"{a.Doctor.FirstName} {a.Doctor.LastName}".Trim(),
            Email = a.Doctor.Email,
            PhotoUrl = a.Doctor.PhotoUrl,
            AssignedAt = a.AssignedAt,
            Notes = a.Notes
        });
    }
}

