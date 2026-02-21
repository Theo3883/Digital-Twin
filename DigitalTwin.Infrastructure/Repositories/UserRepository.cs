using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly HealthAppDbContext _db;

    public UserRepository(HealthAppDbContext db) => _db = db;

    public async Task<User?> GetByIdAsync(long id)
    {
        var entity = await _db.Users.FindAsync(id);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var entity = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(User user)
    {
        var entity = ToEntity(user);
        _db.Users.Add(entity);
        await _db.SaveChangesAsync();
        user.Id = entity.Id;
    }

    public async Task UpdateAsync(User user)
    {
        var entity = await _db.Users.FindAsync(user.Id);
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
        await _db.SaveChangesAsync();
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
