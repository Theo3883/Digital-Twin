using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync.Drainers;

/// <summary>
/// Drains dirty <c>User</c> rows from local SQLite to the cloud database.
/// Uses an upsert strategy keyed on <c>Email</c>: existing cloud users are
/// updated, new users are inserted.
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
            _logger.LogDebug("[{Table}] Cloud repository not configured — skipping.", TableName);
            return 0;
        }

        var dirty = (await _local.GetDirtyAsync()).ToList();
        if (dirty.Count == 0) return 0;

        _logger.LogInformation("[{Table}] Draining {Count} dirty rows to cloud.", TableName, dirty.Count);

        foreach (var user in dirty)
        {
            ct.ThrowIfCancellationRequested();
            await UpsertAsync(user);
        }

        await _local.MarkSyncedAsync(dirty);
        await _local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);

        return dirty.Count;
    }

    private async Task UpsertAsync(User user)
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
}
