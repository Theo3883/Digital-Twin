using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Repositories;

public interface ISleepSessionRepository
{
    Task<IEnumerable<SleepSession>> GetByPatientAsync(
        Guid patientId,
        DateTime? from = null,
        DateTime? to = null);

    Task AddAsync(SleepSession session);

    Task AddRangeAsync(IEnumerable<SleepSession> sessions);

    Task<bool> ExistsAsync(Guid patientId, DateTime startTime);

    Task<IEnumerable<SleepSession>> GetDirtyAsync();

    Task MarkSyncedAsync(Guid patientId, DateTime beforeTimestamp);

    Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc);
}
