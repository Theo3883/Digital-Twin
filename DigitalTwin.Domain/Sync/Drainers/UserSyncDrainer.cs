using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using DigitalTwin.Domain.Sync;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Domain.Sync.Drainers;

/// <summary>
/// Bidirectional sync for <c>User</c> rows.
/// PUSH: dirty local users → cloud (upsert by email).
/// PULL: refresh local user profiles from cloud.
/// </summary>
public sealed class UserSyncDrainer : SyncDrainerBase<User>
{
    private readonly IUserRepository _local;
    private readonly IUserRepository? _cloud;

    public override int Order => 0;
    public override string TableName => "Users";
    protected override bool IsCloudConfigured => _cloud is not null;

    public UserSyncDrainer(
        IUserRepository local,
        IUserRepository? cloud,
        ILogger<UserSyncDrainer> logger) : base(logger)
    {
        _local = local;
        _cloud = cloud;
    }

    // ── Push hooks ───────────────────────────────────────────────────────────

    protected override async Task<List<User>> GetDirtyItemsAsync(CancellationToken ct)
        => (await _local.GetDirtyAsync()).ToList();

    protected override Task<List<User>> MapToCloudBatchAsync(List<User> dirtyItems, CancellationToken ct)
        => Task.FromResult(dirtyItems);

    protected override async Task UpsertToCloudBatchAsync(List<User> cloudItems, CancellationToken ct)
    {
        foreach (var user in cloudItems)
        {
            ct.ThrowIfCancellationRequested();
            var existing = await _cloud!.GetByEmailAsync(user.Email);
            if (existing is not null)
            {
                existing.Role = user.Role;
                existing.FirstName = user.FirstName;
                existing.LastName = user.LastName;
                existing.PhotoUrl = user.PhotoUrl;
                existing.Phone = user.Phone;
                existing.Address = user.Address;
                existing.City = user.City;
                existing.Country = user.Country;
                existing.DateOfBirth = user.DateOfBirth;
                await _cloud.UpdateAsync(existing);
            }
            else
            {
                await _cloud.AddAsync(user);
            }
        }
    }

    protected override async Task MarkPushedAsSyncedAsync(List<User> originalDirtyItems, CancellationToken ct)
        => await _local.MarkSyncedAsync(originalDirtyItems);

    protected override async Task PurgeSyncedAsync(CancellationToken ct)
        => await _local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);

    // ── Pull hooks ───────────────────────────────────────────────────────────

    protected override async Task<IReadOnlyList<PullScope>> GetPullScopesAsync(CancellationToken ct)
    {
        var localUsers = (await _local.GetAllAsync()).ToList();
        var scopes = new List<PullScope>();

        foreach (var localUser in localUsers)
        {
            ct.ThrowIfCancellationRequested();
            var cloudUser = await _cloud!.GetByEmailAsync(localUser.Email);
            if (cloudUser is null) continue;
            scopes.Add(new PullScope(localUser.Id, cloudUser.Id, localUser));
        }

        return scopes;
    }

    protected override async Task<IReadOnlyList<User>> FetchCloudItemsAsync(PullScope scope, CancellationToken ct)
    {
        var cloudUser = await _cloud!.GetByIdAsync(scope.CloudId);
        return cloudUser is null ? [] : [cloudUser];
    }

    protected override async Task<int> MergeCloudItemsToLocalAsync(IReadOnlyList<User> cloudItems, PullScope scope, CancellationToken ct)
    {
        if (cloudItems.Count == 0) return 0;

        var cloudUser = cloudItems[0];
        var localUser = (User)scope.Context!;

        localUser.Role = cloudUser.Role;
        localUser.FirstName = cloudUser.FirstName;
        localUser.LastName = cloudUser.LastName;
        localUser.PhotoUrl = cloudUser.PhotoUrl;
        localUser.Phone = cloudUser.Phone;
        localUser.Address = cloudUser.Address;
        localUser.City = cloudUser.City;
        localUser.Country = cloudUser.Country;
        localUser.DateOfBirth = cloudUser.DateOfBirth;
        await _local.UpdateAsync(localUser);
        return 1;
    }
}
