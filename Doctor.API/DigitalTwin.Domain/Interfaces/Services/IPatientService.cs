using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface IPatientService
{
    Task<Patient> CreateOrUpdateProfileAsync(Guid userId, PatientProfileUpdate update);
    Task<Patient?> GetByUserIdAsync(Guid userId);
    Task<bool> HasPatientProfileAsync(Guid userId);
    Task<Guid?> GetPatientIdForUserAsync(Guid userId);
}
