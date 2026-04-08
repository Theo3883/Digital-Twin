using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Sync;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Domain.Sync.Drainers;

/// <summary>
/// Bidirectional sync for <c>UserOAuth</c> rows.
/// PUSH: dirty local OAuth tokens → cloud (upsert keyed on Provider+ProviderUserId).
/// PULL: for each local user, fetch their OAuth records from cloud and upsert locally.
/// Must run after <see cref="UserSyncDrainer"/>.
/// </summary>
public sealed class UserOAuthSyncDrainer : SyncDrainerBase<UserOAuth>
{
    private readonly IUserOAuthRepository _local;
    private readonly IUserOAuthRepository? _cloud;
    private readonly IUserRepository _user;
    private readonly IUserRepository? _cloudUser;

    public override int Order => 2;
    public override string TableName => "UserOAuth";
    protected override bool IsCloudConfigured => _cloud is not null && _cloudUser is not null;

    public UserOAuthSyncDrainer(
        IUserOAuthRepository local,
        IUserOAuthRepository? cloud,
        IUserRepository user,
        IUserRepository? cloudUser,
        ILogger<UserOAuthSyncDrainer> logger) : base(logger)
    {
        _local = local;
        _cloud = cloud;
        _user = user;
        _cloudUser = cloudUser;
    }

    // ── Push hooks ───────────────────────────────────────────────────────────

    protected override async Task<List<UserOAuth>> GetDirtyItemsAsync(CancellationToken ct)
        => (await _local.GetDirtyAsync()).ToList();

    protected override async Task<List<UserOAuth>> MapToCloudBatchAsync(List<UserOAuth> dirtyItems, CancellationToken ct)
    {
        var result = new List<UserOAuth>();
        foreach (var oauth in dirtyItems)
        {
            ct.ThrowIfCancellationRequested();
            var cloudUserId = await ResolveCloudUserIdAsync(oauth.UserId);
            if (cloudUserId is null)
            {
                Logger.LogWarning("[{Table}] Cloud User not found for local UserId {UserId} — skipped.", TableName, oauth.UserId);
                continue;
            }
            result.Add(new UserOAuth
            {
                UserId = cloudUserId.Value,
                Provider = oauth.Provider,
                ProviderUserId = oauth.ProviderUserId,
                Email = oauth.Email,
                AccessToken = oauth.AccessToken,
                RefreshToken = oauth.RefreshToken,
                ExpiresAt = oauth.ExpiresAt,
                CreatedAt = oauth.CreatedAt,
                UpdatedAt = oauth.UpdatedAt
            });
        }
        return result;
    }

    protected override async Task UpsertToCloudBatchAsync(List<UserOAuth> cloudItems, CancellationToken ct)
    {
        foreach (var oauth in cloudItems)
        {
            ct.ThrowIfCancellationRequested();
            var existing = await _cloud!.FindByProviderAndUserIdAsync(oauth.Provider, oauth.ProviderUserId);
            if (existing is not null)
            {
                existing.UserId = oauth.UserId;
                existing.Email = oauth.Email;
                existing.AccessToken = oauth.AccessToken;
                existing.RefreshToken = oauth.RefreshToken;
                existing.ExpiresAt = oauth.ExpiresAt;
                await _cloud.UpdateAsync(existing);
            }
            else
            {
                await _cloud.AddAsync(oauth);
            }
        }
    }

    protected override async Task MarkPushedAsSyncedAsync(List<UserOAuth> originalDirtyItems, CancellationToken ct)
        => await _local.MarkSyncedAsync(originalDirtyItems);

    protected override async Task PurgeSyncedAsync(CancellationToken ct)
        => await _local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);

    // ── Pull hooks ───────────────────────────────────────────────────────────

    protected override async Task<IReadOnlyList<PullScope>> GetPullScopesAsync(CancellationToken ct)
    {
        var localUsers = (await _user.GetAllAsync()).ToList();
        var scopes = new List<PullScope>();

        foreach (var localUser in localUsers)
        {
            ct.ThrowIfCancellationRequested();
            var cloudUser = await _cloudUser!.GetByEmailAsync(localUser.Email);
            if (cloudUser is null) continue;
            scopes.Add(new PullScope(localUser.Id, cloudUser.Id));
        }

        return scopes;
    }

    protected override async Task<IReadOnlyList<UserOAuth>> FetchCloudItemsAsync(PullScope scope, CancellationToken ct)
        => (await _cloud!.GetByUserIdAsync(scope.CloudId)).ToList();

    protected override async Task<int> MergeCloudItemsToLocalAsync(
        IReadOnlyList<UserOAuth> cloudItems, PullScope scope, CancellationToken ct)
    {
        return await UpsertEachAsync(
            cloudItems,
            findLocalAsync: async cloudToken =>
                await _local.FindByProviderAndUserIdAsync(cloudToken.Provider, cloudToken.ProviderUserId),
            updateLocalAsync: async (local, cloudToken) =>
            {
                local.UserId = scope.LocalId;
                local.Email = cloudToken.Email;
                local.AccessToken = cloudToken.AccessToken;
                local.RefreshToken = cloudToken.RefreshToken;
                local.ExpiresAt = cloudToken.ExpiresAt;
                await _local.UpdateAsync(local);
            },
            addLocalAsync: async cloudToken =>
            {
                await _local.AddAsync(new UserOAuth
                {
                    UserId = scope.LocalId,
                    Provider = cloudToken.Provider,
                    ProviderUserId = cloudToken.ProviderUserId,
                    Email = cloudToken.Email,
                    AccessToken = cloudToken.AccessToken,
                    RefreshToken = cloudToken.RefreshToken,
                    ExpiresAt = cloudToken.ExpiresAt,
                    CreatedAt = cloudToken.CreatedAt,
                    UpdatedAt = cloudToken.UpdatedAt
                });
            },
            ct);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<Guid?> ResolveCloudUserIdAsync(Guid localUserId)
    {
        var localUser = await _user.GetByIdAsync(localUserId);
        if (localUser is null) return null;
        var cloudUser = await _cloudUser!.GetByEmailAsync(localUser.Email);
        return cloudUser?.Id;
    }
}
