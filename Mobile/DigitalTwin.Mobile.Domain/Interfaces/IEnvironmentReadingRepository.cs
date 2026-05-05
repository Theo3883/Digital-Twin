using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface IEnvironmentReadingRepository
{
    Task SaveAsync(EnvironmentReading reading);
    Task<EnvironmentReading?> GetLatestAsync();
    Task<IEnumerable<EnvironmentReading>> GetSinceAsync(DateTime since, int limit = 200);
    Task<IEnumerable<EnvironmentReading>> GetDirtyAsync();
    Task MarkSyncedAsync(DateTime beforeOrAtTimestamp);
}
