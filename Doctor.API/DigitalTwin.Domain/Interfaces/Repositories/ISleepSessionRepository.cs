using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Repositories;

public interface ISleepSessionRepository
{
    Task<IEnumerable<SleepSession>> GetByPatientAsync(
        Guid patientId,
        DateTime? from = null,
        DateTime? to = null);

    Task AddAsync(SleepSession session, bool markDirty = true);

    Task AddRangeAsync(IEnumerable<SleepSession> sessions, bool markDirty = true);

    Task<bool> ExistsAsync(Guid patientId, DateTime startTime);

    Task<IEnumerable<SleepSession>> GetDirtyAsync();

    Task MarkSyncedAsync(Guid patientId, DateTime beforeTimestamp);

    Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc);
}
