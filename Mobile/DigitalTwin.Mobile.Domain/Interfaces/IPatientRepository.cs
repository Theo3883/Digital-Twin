using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

/// <summary>
/// Domain interface for patient data access in mobile app
/// </summary>
public interface IPatientRepository
{
    Task<Patient?> GetByIdAsync(Guid id);
    Task<Patient?> GetByUserIdAsync(Guid userId);
    Task<Patient?> GetCurrentPatientAsync();
    Task SaveAsync(Patient patient);
    Task<IEnumerable<Patient>> GetUnsyncedAsync();
    Task MarkAsSyncedAsync(Guid id);
}