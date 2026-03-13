using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync.Drainers;

/// <summary>
/// Bidirectional sync for <c>User</c> rows.
///
/// PUSH: dirty local users → cloud (upsert by email).
/// PULL: for each local user, refresh their profile from the cloud so changes
///       made on other devices (e.g. name, photo) appear locally.
/// </summary>
public sealed class UserDrainer : ITableDrainer
{
    private static readonly TimeSpan PurgeOlderThan = TimeSpan.FromDays(7);

    private readonly IUserRepository _local;
    private readonly IUserRepository? _cloud;
    private readonly ILogger<UserDrainer> _logger;

    public int Order => 0;
    public string TableName => "Users";

    public UserDrainer(
        IUserRepository local,
        IUserRepository? cloud,
        ILogger<UserDrainer> logger)
    {
        _local = local;
        _cloud = cloud;
        _logger = logger;
    }

    public async Task<int> DrainAsync(CancellationToken ct = default)
    {
        if (_cloud is null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("[{Table}] Cloud repository not configured — skipping.", TableName);
            return 0;
        }

        var pushed = await PushAsync(ct);
        var pulled = await PullAsync(ct);
        return pushed + pulled;
    }

    // ── PUSH: local dirty → cloud ────────────────────────────────────────────

    private async Task<int> PushAsync(CancellationToken ct)
    {
        var dirty = (await _local.GetDirtyAsync()).ToList();
        if (dirty.Count == 0) return 0;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[{Table}] Pushing {Count} dirty rows to cloud.", TableName, dirty.Count);

        foreach (var user in dirty)
        {
            ct.ThrowIfCancellationRequested();
            await UpsertToCloudAsync(user);
        }

        await _local.MarkSyncedAsync(dirty);
        await _local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);
        return dirty.Count;
    }

    private async Task UpsertToCloudAsync(User user)
    {
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

    // ── PULL: cloud → local (scoped to this device's users) ─────────────────
    // Only the users stored locally (i.e. the current device user) are refreshed.

    private async Task<int> PullAsync(CancellationToken ct)
    {
        var localUsers = (await _local.GetAllAsync()).ToList();
        int count = 0;

        foreach (var localUser in localUsers)
        {
            ct.ThrowIfCancellationRequested();
            var cloudUser = await _cloud!.GetByEmailAsync(localUser.Email);
            if (cloudUser is null) continue;

            // Refresh local profile fields that the server is authoritative for.
            localUser.Role        = cloudUser.Role;
            localUser.FirstName   = cloudUser.FirstName;
            localUser.LastName    = cloudUser.LastName;
            localUser.PhotoUrl    = cloudUser.PhotoUrl;
            localUser.Phone       = cloudUser.Phone;
            localUser.Address     = cloudUser.Address;
            localUser.City        = cloudUser.City;
            localUser.Country     = cloudUser.Country;
            localUser.DateOfBirth = cloudUser.DateOfBirth;
            await _local.UpdateAsync(localUser);
            count++;
        }

        if (count > 0 && _logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[{Table}] Pulled and refreshed {Count} local users from cloud.", TableName, count);

        return count;
    }
}
