using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Services;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Services;

public class DoctorPatientAssignmentService : IDoctorPatientAssignmentService
{
    private readonly IDoctorPatientAssignmentRepository _assignments;
    private readonly IUserRepository _users;

    public DoctorPatientAssignmentService(
        IDoctorPatientAssignmentRepository assignments,
        IUserRepository users)
    {
        _assignments = assignments;
        _users = users;
    }

    public async Task<IEnumerable<AssignedDoctorInfo>> GetAssignedDoctorsAsync(string patientEmail)
    {
        var assignments = await _assignments.GetByPatientEmailAsync(patientEmail);
        var result = new List<AssignedDoctorInfo>();

        foreach (var a in assignments.OrderByDescending(x => x.AssignedAt))
        {
            var doctor = await _users.GetByIdAsync(a.DoctorId);
            if (doctor is null) continue;

            result.Add(new AssignedDoctorInfo
            {
                Doctor = doctor,
                AssignedAt = a.AssignedAt,
                Notes = a.Notes
            });
        }

        return result;
    }
}

