using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Sync;

public class PatientLocalSyncStore : ILocalSyncStore<Patient>
{
    private readonly IPatientRepository _repo;

    public PatientLocalSyncStore(IPatientRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<Patient>> GetDirtyAsync()
    {
        var items = await _repo.GetDirtyAsync();
        return items.ToList();
    }

    public async Task MarkSyncedAsync(IEnumerable<Patient> items)
    {
        await _repo.MarkSyncedAsync(items);
    }

    public async Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc)
    {
        await _repo.PurgeSyncedOlderThanAsync(cutoffUtc);
    }
}
