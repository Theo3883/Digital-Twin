using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Sync;

public class VitalSignLocalSyncStore : ILocalSyncStore<VitalSign>
{
    private readonly IVitalSignRepository _localRepo;

    public VitalSignLocalSyncStore(IVitalSignRepository localRepo)
    {
        _localRepo = localRepo;
    }

    public async Task<IReadOnlyList<VitalSign>> GetDirtyAsync()
    {
        var items = await _localRepo.GetDirtyAsync();
        return items.ToList();
    }

    public async Task MarkSyncedAsync(IEnumerable<VitalSign> items)
    {
        var grouped = items.GroupBy(v => v.PatientId);
        foreach (var group in grouped)
        {
            var maxTs = group.Max(v => v.Timestamp);
            await _localRepo.MarkSyncedAsync(group.Key, maxTs);
        }
    }

    public async Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc)
    {
        await _localRepo.PurgeSyncedOlderThanAsync(cutoffUtc);
    }
}
