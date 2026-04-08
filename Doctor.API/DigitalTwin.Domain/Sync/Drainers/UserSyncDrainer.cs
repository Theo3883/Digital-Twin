using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Sync;
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
    private readonly IUserSyncClient? _syncClient;

    public override int Order => 0;
    public override string TableName => "Users";
    protected override bool IsCloudConfigured => _syncClient is not null;

    public UserSyncDrainer(
        IUserRepository local,
        IUserSyncClient? syncClient,
        ILogger<UserSyncDrainer> logger) : base(logger)
    {
        _local = local;
        _syncClient = syncClient;
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
            
            var success = await _syncClient!.PushUserAsync(user, ct);
            if (!success)
            {
                Logger.LogError("[UserSyncDrainer] Failed to sync user {Email}", user.Email);
                throw new InvalidOperationException($"Failed to sync user {user.Email}");
            }
        }
    }

    protected override async Task MarkPushedAsSyncedAsync(List<User> originalDirtyItems, CancellationToken ct)
        => await _local.MarkSyncedAsync(originalDirtyItems);

    protected override async Task PurgeSyncedAsync(CancellationToken ct)
        => await _local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);

    // ── Pull hooks ───────────────────────────────────────────────────────────
    // Note: User pull is not implemented in the mobile sync API as users are typically
    // managed through authentication flows. If needed, this could be added later.

    protected override Task<IReadOnlyList<PullScope>> GetPullScopesAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PullScope>>([]);

    protected override Task<IReadOnlyList<User>> FetchCloudItemsAsync(PullScope scope, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<User>>([]);

    protected override Task<int> MergeCloudItemsToLocalAsync(IReadOnlyList<User> cloudItems, PullScope scope, CancellationToken ct)
        => Task.FromResult(0);

}
