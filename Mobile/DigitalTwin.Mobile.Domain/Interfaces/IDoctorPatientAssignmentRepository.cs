using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface IDoctorPatientAssignmentRepository
{
    Task<IReadOnlyList<AssignedDoctor>> GetByUserIdAsync(Guid userId);
    Task ReplaceForUserAsync(Guid userId, IEnumerable<AssignedDoctor> doctors);
}
