using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Repositories;

public interface IVitalSignRepository
{
    Task<IEnumerable<VitalSign>> GetByPatientAsync(
        Guid patientId,
        VitalSignType? type = null,
        DateTime? from = null,
        DateTime? to = null);

    Task AddAsync(VitalSign vitalSign);

    Task AddRangeAsync(IEnumerable<VitalSign> vitalSigns);

    Task<bool> ExistsAsync(Guid patientId, VitalSignType type, DateTime timestamp);

    Task<IEnumerable<VitalSign>> GetDirtyAsync();

    Task MarkSyncedAsync(Guid patientId, DateTime beforeTimestamp);

    Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc);
}
