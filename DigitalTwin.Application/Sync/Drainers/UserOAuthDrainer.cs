using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync.Drainers;

/// <summary>
/// Drains dirty <c>UserOAuth</c> rows from local SQLite to the cloud database.
/// Uses an upsert strategy keyed on <c>(Provider, ProviderUserId)</c>: existing
/// cloud tokens are updated (refreshed), new OAuth links are inserted.
/// </summary>
public sealed class UserOAuthDrainer : ITableDrainer
{
    private static readonly TimeSpan PurgeOlderThan = TimeSpan.FromDays(7);

    private readonly IUserOAuthRepository _local;
    private readonly IUserOAuthRepository? _cloud;
    private readonly ILogger<UserOAuthDrainer> _logger;

    public string TableName => "UserOAuth";

    public UserOAuthDrainer(
        IUserOAuthRepository local,
        IUserOAuthRepository? cloud,
        ILogger<UserOAuthDrainer> logger)
    {
        _local = local;
        _cloud = cloud;
        _logger = logger;
    }

    public async Task<int> DrainAsync(CancellationToken ct = default)
    {
        if (_cloud is null)
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
