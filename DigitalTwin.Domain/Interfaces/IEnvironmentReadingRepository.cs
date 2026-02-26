using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface IEnvironmentReadingRepository
{
    Task AddAsync(EnvironmentReading reading);
    Task AddRangeAsync(IEnumerable<EnvironmentReading> readings);
    Task<IEnumerable<EnvironmentReading>> GetDirtyAsync();
    Task MarkSyncedAsync(DateTime beforeOrAtTimestamp);
    Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc);
}
