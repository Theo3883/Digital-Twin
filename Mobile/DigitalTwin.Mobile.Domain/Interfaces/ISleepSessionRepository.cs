using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface ISleepSessionRepository
{
    Task<IEnumerable<SleepSession>> GetByPatientIdAsync(Guid patientId, DateTime? from = null, DateTime? to = null);
    Task SaveAsync(SleepSession session);
    Task SaveRangeAsync(IEnumerable<SleepSession> sessions);
    Task<bool> ExistsAsync(Guid patientId, DateTime startTime);
    Task<IEnumerable<SleepSession>> GetUnsyncedAsync();
    Task MarkAsSyncedAsync(Guid patientId, DateTime beforeTimestamp);
}
