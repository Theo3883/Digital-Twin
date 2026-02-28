using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly Func<HealthAppDbContext> _factory;
    private readonly bool _markDirtyOnInsert;

    public UserRepository(Func<HealthAppDbContext> factory, bool markDirtyOnInsert = true)
    {
        _factory = factory;
        _markDirtyOnInsert = markDirtyOnInsert;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        await using var db = _factory();
        var entity = await db.Users.FindAsync(id);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        await using var db = _factory();
        var entity = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(User user)
    {
        await using var db = _factory();
        var entity = ToEntity(user);
        entity.IsDirty = _markDirtyOnInsert;
        if (!_markDirtyOnInsert) entity.SyncedAt = DateTime.UtcNow;
        db.Users.Add(entity);
        await db.SaveChangesAsync();
        user.Id = entity.Id;
    }

    public async Task UpdateAsync(User user)
    {
        await using var db = _factory();
        var entity = await db.Users.FindAsync(user.Id);
        if (entity is null) return;

        entity.Email = user.Email;
        entity.Role = (int)user.Role;
        entity.FirstName = user.FirstName;
        entity.LastName = user.LastName;
        entity.PhotoUrl = user.PhotoUrl;
        entity.Phone = user.Phone;
        entity.Address = user.Address;
        entity.City = user.City;
        entity.Country = user.Country;
        entity.DateOfBirth = user.DateOfBirth;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.IsDirty = _markDirtyOnInsert;
        if (!_markDirtyOnInsert) entity.SyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<User>> GetDirtyAsync()
    {
        await using var db = _factory();
        var entities = await db.Users.Where(u => u.IsDirty).ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task MarkSyncedAsync(IEnumerable<User> items)
    {
        await using var db = _factory();
        var ids = items.Select(u => u.Id).ToHashSet();
        await db.Users
            .Where(u => ids.Contains(u.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.IsDirty, false)
                .SetProperty(u => u.SyncedAt, DateTime.UtcNow));
    }

    public async Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc)
    {
        await using var db = _factory();
        await db.Users
            .Where(u => !u.IsDirty && u.SyncedAt.HasValue && u.SyncedAt.Value < cutoffUtc)
            .ExecuteDeleteAsync();
    }

    public async Task<bool> ExistsAsync(User user)
    {
        await using var db = _factory();
        return await db.Users.AnyAsync(u => u.Email == user.Email);
    }

    private static User ToDomain(UserEntity entity) => new()
    {
        Id = entity.Id,
        Email = entity.Email,
        Role = (UserRole)entity.Role,
        FirstName = entity.FirstName,
        LastName = entity.LastName,
        PhotoUrl = entity.PhotoUrl,
        Phone = entity.Phone,
        Address = entity.Address,
        City = entity.City,
        Country = entity.Country,
        DateOfBirth = entity.DateOfBirth,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static UserEntity ToEntity(User model) => new()
    {
        Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id,
        Email = model.Email,
        Role = (int)model.Role,
        FirstName = model.FirstName,
        LastName = model.LastName,
        PhotoUrl = model.PhotoUrl,
        Phone = model.Phone,
        Address = model.Address,
        City = model.City,
        Country = model.Country,
        DateOfBirth = model.DateOfBirth,
        CreatedAt = model.CreatedAt,
        UpdatedAt = model.UpdatedAt
    };
}
