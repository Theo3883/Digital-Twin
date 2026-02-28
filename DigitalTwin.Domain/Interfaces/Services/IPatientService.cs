using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface IPatientService
{
    Task<Patient> CreateOrUpdateProfileAsync(
        long userId, string? bloodType, string? allergies, string? medicalHistoryNotes);
    Task<Patient?> GetByUserIdAsync(long userId);
    Task<bool> HasPatientProfileAsync(long userId);
    Task<long?> GetPatientIdForUserAsync(long userId);
}
