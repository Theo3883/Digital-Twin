using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Sync;

public class UserOAuthLocalSyncStore : ILocalSyncStore<UserOAuth>
{
    private readonly IUserOAuthRepository _repo;

    public UserOAuthLocalSyncStore(IUserOAuthRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<UserOAuth>> GetDirtyAsync()
    {
        var items = await _repo.GetDirtyAsync();
        return items.ToList();
    }

    public async Task MarkSyncedAsync(IEnumerable<UserOAuth> items)
    {
        await _repo.MarkSyncedAsync(items);
    }

    public async Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc)
    {
        await _repo.PurgeSyncedOlderThanAsync(cutoffUtc);
    }
}
