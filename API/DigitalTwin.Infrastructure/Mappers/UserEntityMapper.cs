using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Entities;

namespace DigitalTwin.Infrastructure.Mappers;

internal static class UserEntityMapper
{
    internal static User ToDomain(UserEntity e) => new()
    {
        Id        = e.Id,
        Email     = e.Email,
        Role      = (UserRole)e.Role,
        FirstName = e.FirstName,
        LastName  = e.LastName,
        PhotoUrl  = e.PhotoUrl,
        Phone     = e.Phone,
        Address   = e.Address,
        City      = e.City,
        Country   = e.Country,
        DateOfBirth = e.DateOfBirth,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    internal static UserEntity ToEntity(User m) => new()
    {
        Id        = m.Id,
        Email     = m.Email,
        Role      = (int)m.Role,
        FirstName = m.FirstName,
        LastName  = m.LastName,
        PhotoUrl  = m.PhotoUrl,
        Phone     = m.Phone,
        Address   = m.Address,
        City      = m.City,
        Country   = m.Country,
        DateOfBirth = m.DateOfBirth,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };
}
