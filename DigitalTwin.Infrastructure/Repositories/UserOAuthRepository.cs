using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Repositories;

public class UserOAuthRepository : IUserOAuthRepository
{
    private readonly Func<HealthAppDbContext> _factory;
    private readonly bool _markDirtyOnInsert;

    public UserOAuthRepository(Func<HealthAppDbContext> factory, bool markDirtyOnInsert = true)
    {
        _factory = factory;
        _markDirtyOnInsert = markDirtyOnInsert;
    }

    public async Task<UserOAuth?> FindByProviderAndUserIdAsync(OAuthProvider provider, string providerUserId)
    {
        await using var db = _factory();
        var entity = await db.UserOAuths
            .FirstOrDefaultAsync(o => o.Provider == (int)provider && o.ProviderUserId == providerUserId);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(UserOAuth userOAuth)
    {
        await using var db = _factory();
        var entity = ToEntity(userOAuth);
        entity.IsDirty = _markDirtyOnInsert;
        if (!_markDirtyOnInsert) entity.SyncedAt = DateTime.UtcNow;
        db.UserOAuths.Add(entity);
        await db.SaveChangesAsync();
        userOAuth.Id = entity.Id;
    }

    public async Task UpdateAsync(UserOAuth userOAuth)
    {
        await using var db = _factory();
        var entity = await db.UserOAuths.FindAsync(userOAuth.Id);
        if (entity is null) return;

        entity.AccessToken = userOAuth.AccessToken;
        entity.RefreshToken = userOAuth.RefreshToken;
        entity.ExpiresAt = userOAuth.ExpiresAt;
        entity.Email = userOAuth.Email;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.IsDirty = _markDirtyOnInsert;
        if (!_markDirtyOnInsert) entity.SyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<UserOAuth>> GetDirtyAsync()
    {
        await using var db = _factory();
        var entities = await db.UserOAuths.Where(o => o.IsDirty).ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task MarkSyncedAsync(IEnumerable<UserOAuth> items)
    {
        await using var db = _factory();
        var ids = items.Select(o => o.Id).ToHashSet();
        await db.UserOAuths
            .Where(o => ids.Contains(o.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.IsDirty, false)
                .SetProperty(o => o.SyncedAt, DateTime.UtcNow));
    }

    public async Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc)
    {
        await using var db = _factory();
        await db.UserOAuths
            .Where(o => !o.IsDirty && o.SyncedAt.HasValue && o.SyncedAt.Value < cutoffUtc)
            .ExecuteDeleteAsync();
    }

    public async Task<bool> ExistsAsync(UserOAuth userOAuth)
    {
        await using var db = _factory();
        return await db.UserOAuths.AnyAsync(o =>
            o.Provider == (int)userOAuth.Provider && o.ProviderUserId == userOAuth.ProviderUserId);
    }

    private static UserOAuth ToDomain(UserOAuthEntity entity) => new()
    {
        Id = entity.Id,
        UserId = entity.UserId,
        Provider = (OAuthProvider)entity.Provider,
        ProviderUserId = entity.ProviderUserId,
        Email = entity.Email,
        AccessToken = entity.AccessToken,
        RefreshToken = entity.RefreshToken,
        ExpiresAt = entity.ExpiresAt,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static UserOAuthEntity ToEntity(UserOAuth model) => new()
    {
        Id = model.Id,
        UserId = model.UserId,
        Provider = (int)model.Provider,
        ProviderUserId = model.ProviderUserId,
        Email = model.Email,
        AccessToken = model.AccessToken,
        RefreshToken = model.RefreshToken,
        ExpiresAt = model.ExpiresAt,
        CreatedAt = model.CreatedAt,
        UpdatedAt = model.UpdatedAt
    };
}
