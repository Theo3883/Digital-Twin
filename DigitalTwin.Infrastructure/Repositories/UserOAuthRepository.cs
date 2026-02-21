using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Repositories;

public class UserOAuthRepository : IUserOAuthRepository
{
    private readonly HealthAppDbContext _db;

    public UserOAuthRepository(HealthAppDbContext db) => _db = db;

    public async Task<UserOAuth?> FindByProviderAndUserIdAsync(OAuthProvider provider, string providerUserId)
    {
        var entity = await _db.UserOAuths
            .FirstOrDefaultAsync(o => o.Provider == (int)provider && o.ProviderUserId == providerUserId);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(UserOAuth userOAuth)
    {
        var entity = ToEntity(userOAuth);
        _db.UserOAuths.Add(entity);
        await _db.SaveChangesAsync();
        userOAuth.Id = entity.Id;
    }

    public async Task UpdateAsync(UserOAuth userOAuth)
    {
        var entity = await _db.UserOAuths.FindAsync(userOAuth.Id);
        if (entity is null) return;

        entity.AccessToken = userOAuth.AccessToken;
        entity.RefreshToken = userOAuth.RefreshToken;
        entity.ExpiresAt = userOAuth.ExpiresAt;
        entity.Email = userOAuth.Email;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
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
