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

    /// <param name="markDirty">
    /// When <c>true</c> (default) rows are stored with IsDirty=true so the drain
    /// timer will push them to the cloud. Pass <c>false</c> when the records have
    /// already been written to the cloud and should be kept locally as a synced
    /// archive without triggering another upload.
    /// </param>
    Task AddRangeAsync(IEnumerable<VitalSign> vitalSigns, bool markDirty = true);

    Task<bool> ExistsAsync(Guid patientId, VitalSignType type, DateTime timestamp);

    Task<IEnumerable<VitalSign>> GetDirtyAsync();

    Task MarkSyncedAsync(Guid patientId, DateTime beforeTimestamp);

    Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc);
}
