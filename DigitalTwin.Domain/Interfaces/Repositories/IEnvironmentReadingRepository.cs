using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Repositories;

public interface IEnvironmentReadingRepository
{
    Task AddAsync(EnvironmentReading reading, bool markDirty = true);
    Task AddRangeAsync(IEnumerable<EnvironmentReading> readings, bool markDirty = true);
    Task<IEnumerable<EnvironmentReading>> GetDirtyAsync();
    Task<IEnumerable<EnvironmentReading>> GetSinceAsync(DateTime since, int limit = 200);
    Task<bool> ExistsAsync(DateTime timestamp);
    Task MarkSyncedAsync(DateTime beforeOrAtTimestamp);
    Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc);
}
