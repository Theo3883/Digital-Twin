using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync.Drainers;

/// <summary>
/// Drains dirty <c>UserOAuth</c> rows from local SQLite to the cloud database.
/// Must run after <see cref="UserDrainer"/>. Maps local UserId → cloud UserId via Email.
/// Uses an upsert strategy keyed on <c>(Provider, ProviderUserId)</c>.
/// </summary>
public sealed class UserOAuthDrainer : ITableDrainer
{
    private static readonly TimeSpan PurgeOlderThan = TimeSpan.FromDays(7);

    private readonly IUserOAuthRepository _local;
    private readonly IUserOAuthRepository? _cloud;
    private readonly IUserRepository _localUser;
    private readonly IUserRepository? _cloudUser;
    private readonly ILogger<UserOAuthDrainer> _logger;

    public int Order => 2;
    public string TableName => "UserOAuth";

    public UserOAuthDrainer(
        IUserOAuthRepository local,
        IUserOAuthRepository? cloud,
        IUserRepository localUser,
        IUserRepository? cloudUser,
        ILogger<UserOAuthDrainer> logger)
    {
        _local = local;
        _cloud = cloud;
        _localUser = localUser;
        _cloudUser = cloudUser;
        _logger = logger;
    }

    public async Task<int> DrainAsync(CancellationToken ct = default)
    {
        if (_cloud is null || _cloudUser is null)
        {
            _logger.LogDebug("[{Table}] Cloud repository not configured — skipping.", TableName);
            return 0;
        }

        var dirty = (await _local.GetDirtyAsync()).ToList();
        if (dirty.Count == 0) return 0;

        _logger.LogInformation("[{Table}] Draining {Count} dirty rows to cloud.", TableName, dirty.Count);

        foreach (var oauth in dirty)
        {
            ct.ThrowIfCancellationRequested();
            await UpsertAsync(oauth);
        }

        await _local.MarkSyncedAsync(dirty);
        await _local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);

        return dirty.Count;
    }

    private async Task UpsertAsync(UserOAuth oauth)
    {
        var cloudUserId = await ResolveCloudUserIdAsync(oauth.UserId);
        if (cloudUserId is null)
        {
            _logger.LogWarning("[{Table}] Cloud User not found for local UserId {UserId} — ensure UserDrainer runs first.", TableName, oauth.UserId);
            return;
        }

        var existing = await _cloud!.FindByProviderAndUserIdAsync(oauth.Provider, oauth.ProviderUserId);
        if (existing is not null)
        {
            existing.UserId = cloudUserId.Value;
            existing.Email = oauth.Email;
            existing.AccessToken = oauth.AccessToken;
            existing.RefreshToken = oauth.RefreshToken;
            existing.ExpiresAt = oauth.ExpiresAt;
            await _cloud.UpdateAsync(existing);
        }
        else
        {
            var cloudOAuth = new UserOAuth
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
            };
            await _cloud.AddAsync(cloudOAuth);
        }
    }

    private async Task<long?> ResolveCloudUserIdAsync(long localUserId)
    {
        var localUser = await _localUser.GetByIdAsync(localUserId);
        if (localUser is null) return null;
        var cloudUser = await _cloudUser!.GetByEmailAsync(localUser.Email);
        return cloudUser?.Id;
    }
}
