using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface IVitalSignRepository
{
    Task<IEnumerable<VitalSign>> GetByPatientAsync(
        long patientId,
        VitalSignType? type = null,
        DateTime? from = null,
        DateTime? to = null);

    Task AddAsync(VitalSign vitalSign);

    Task AddRangeAsync(IEnumerable<VitalSign> vitalSigns);

    Task<bool> ExistsAsync(long patientId, VitalSignType type, DateTime timestamp);

    Task<IEnumerable<VitalSign>> GetDirtyAsync();

    Task MarkSyncedAsync(long patientId, DateTime beforeTimestamp);

    Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc);
}
