using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface IPatientRepository
{
    Task<Patient?> GetByUserIdAsync(long userId);
    Task AddAsync(Patient patient);
    Task UpdateAsync(Patient patient);
    Task<IEnumerable<Patient>> GetDirtyAsync();
    Task MarkSyncedAsync(IEnumerable<Patient> items);
    Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc);
    Task<bool> ExistsAsync(Patient patient);
}
