using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Sync;

public class UserLocalSyncStore : ILocalSyncStore<User>
{
    private readonly IUserRepository _repo;

    public UserLocalSyncStore(IUserRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<User>> GetDirtyAsync()
    {
        var items = await _repo.GetDirtyAsync();
        return items.ToList();
    }

    public async Task MarkSyncedAsync(IEnumerable<User> items)
    {
        await _repo.MarkSyncedAsync(items);
    }

    public async Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc)
    {
        await _repo.PurgeSyncedOlderThanAsync(cutoffUtc);
    }
}
