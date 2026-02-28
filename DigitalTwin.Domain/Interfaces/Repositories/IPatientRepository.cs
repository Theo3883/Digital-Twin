using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Repositories;

public interface IPatientRepository
{
    Task<Patient?> GetByIdAsync(long id);
    Task<Patient?> GetByUserIdAsync(long userId);
    Task<IEnumerable<Patient>> GetAllAsync();
    Task AddAsync(Patient patient);
    Task UpdateAsync(Patient patient);
    Task<IEnumerable<Patient>> GetDirtyAsync();
    Task MarkSyncedAsync(IEnumerable<Patient> items);
    Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc);
    Task<bool> ExistsAsync(Patient patient);
}
