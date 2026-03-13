using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync.Drainers;

/// <summary>
/// Bidirectional sync for <c>UserOAuth</c> rows.
///
/// PUSH: dirty local OAuth tokens → cloud (upsert keyed on Provider+ProviderUserId).
/// PULL: for each local user, fetch their OAuth records from cloud and upsert locally
///       so tokens refreshed on other devices (e.g. web) appear on this device.
/// Must run after <see cref="UserDrainer"/>.
/// </summary>
public sealed class UserOAuthDrainer(
    IUserOAuthRepository local,
    IUserOAuthRepository? cloud,
    IUserRepository user,
    IUserRepository? cloudUser,
    ILogger<UserOAuthDrainer> logger)
    : ITableDrainer
{
    private static readonly TimeSpan PurgeOlderThan = TimeSpan.FromDays(7);

    public int Order => 2;
    public string TableName => "UserOAuth";

    public async Task<int> DrainAsync(CancellationToken ct = default)
    {
        if (cloud is null || cloudUser is null)
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("[{Table}] Cloud repository not configured — skipping.", TableName);
            return 0;
        }

        var pushed = await PushAsync(ct);
        var pulled = await PullAsync(ct);
        return pushed + pulled;
    }

    // ── PUSH ─────────────────────────────────────────────────────────────────

    private async Task<int> PushAsync(CancellationToken ct)
    {
        var dirty = (await local.GetDirtyAsync()).ToList();
        if (dirty.Count == 0) return 0;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("[{Table}] Pushing {Count} dirty rows to cloud.", TableName, dirty.Count);

        foreach (var oauth in dirty)
        {
            ct.ThrowIfCancellationRequested();
            await UpsertToCloudAsync(oauth);
        }

        await local.MarkSyncedAsync(dirty);
        await local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);
        return dirty.Count;
    }

    private async Task UpsertToCloudAsync(UserOAuth oauth)
    {
        var cloudUserId = await ResolveCloudUserIdAsync(oauth.UserId);
        if (cloudUserId is null)
        {
            logger.LogWarning("[{Table}] Cloud User not found for local UserId {UserId} — ensure UserDrainer runs first.", TableName, oauth.UserId);
            return;
        }

        var existing = await cloud!.FindByProviderAndUserIdAsync(oauth.Provider, oauth.ProviderUserId);
        if (existing is not null)
        {
            existing.UserId = cloudUserId.Value;
            existing.Email = oauth.Email;
            existing.AccessToken = oauth.AccessToken;
            existing.RefreshToken = oauth.RefreshToken;
            existing.ExpiresAt = oauth.ExpiresAt;
            await cloud.UpdateAsync(existing);
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
            await cloud.AddAsync(cloudOAuth);
        }
    }

    // ── PULL ──────────────────────────────────────────────────────────────────
    // Scoped to local users only — never fetches other users' tokens.

    private async Task<int> PullAsync(CancellationToken ct)
    {
        var localUsers = (await user.GetAllAsync()).ToList();
        int count = 0;

        foreach (var localUser in localUsers)
        {
            ct.ThrowIfCancellationRequested();

            var cloudUser1 = await cloudUser!.GetByEmailAsync(localUser.Email);
            if (cloudUser1 is null) continue;

            var cloudTokens = (await cloud!.GetByUserIdAsync(cloudUser1.Id)).ToList();

            foreach (var cloudToken in cloudTokens)
            {
                ct.ThrowIfCancellationRequested();
                var local1 = await local.FindByProviderAndUserIdAsync(cloudToken.Provider, cloudToken.ProviderUserId);
                if (local1 is not null)
                {
                    // Refresh token data — remap cloud UserId → local UserId.
                    local1.UserId = localUser.Id;
                    local1.Email = cloudToken.Email;
                    local1.AccessToken = cloudToken.AccessToken;
                    local1.RefreshToken = cloudToken.RefreshToken;
                    local1.ExpiresAt = cloudToken.ExpiresAt;
                    await local.UpdateAsync(local1);
                }
                else
                {
                    await local.AddAsync(new UserOAuth
                    {
                        UserId = localUser.Id,
                        Provider = cloudToken.Provider,
                        ProviderUserId = cloudToken.ProviderUserId,
                        Email = cloudToken.Email,
                        AccessToken = cloudToken.AccessToken,
                        RefreshToken = cloudToken.RefreshToken,
                        ExpiresAt = cloudToken.ExpiresAt,
                        CreatedAt = cloudToken.CreatedAt,
                        UpdatedAt = cloudToken.UpdatedAt
                    });
                }
                count++;
            }
        }

        if (count > 0 && logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("[{Table}] Pulled {Count} OAuth records from cloud.", TableName, count);

        return count;
    }

    private async Task<Guid?> ResolveCloudUserIdAsync(Guid localUserId)
    {
        var localUser = await user.GetByIdAsync(localUserId);
        if (localUser is null) return null;
        var cloudUser1 = await cloudUser!.GetByEmailAsync(localUser.Email);
        return cloudUser1?.Id;
    }
}
