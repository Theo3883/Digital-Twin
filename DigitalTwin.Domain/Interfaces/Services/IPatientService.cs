using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface IPatientService
{
    Task<Patient> CreateOrUpdateProfileAsync(
        Guid userId, string? bloodType, string? allergies, string? medicalHistoryNotes);
    Task<Patient?> GetByUserIdAsync(Guid userId);
    Task<bool> HasPatientProfileAsync(Guid userId);
    Task<Guid?> GetPatientIdForUserAsync(Guid userId);
}
