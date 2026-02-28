using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Repositories;

public interface ISleepSessionRepository
{
    Task<IEnumerable<SleepSession>> GetByPatientAsync(
        long patientId,
        DateTime? from = null,
        DateTime? to = null);

    Task AddAsync(SleepSession session);

    Task AddRangeAsync(IEnumerable<SleepSession> sessions);

    Task<bool> ExistsAsync(long patientId, DateTime startTime);

    Task<IEnumerable<SleepSession>> GetDirtyAsync();

    Task MarkSyncedAsync(long patientId, DateTime beforeTimestamp);

    Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc);
}
