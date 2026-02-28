using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Entities;

namespace DigitalTwin.Infrastructure.Mappers;

internal static class UserOAuthEntityMapper
{
    internal static UserOAuth ToDomain(UserOAuthEntity e) => new()
    {
        Id             = e.Id,
        UserId         = e.UserId,
        Provider       = (OAuthProvider)e.Provider,
        ProviderUserId = e.ProviderUserId,
        Email          = e.Email,
        AccessToken    = e.AccessToken,
        RefreshToken   = e.RefreshToken,
        ExpiresAt      = e.ExpiresAt,
        CreatedAt      = e.CreatedAt,
        UpdatedAt      = e.UpdatedAt
    };

    internal static UserOAuthEntity ToEntity(UserOAuth m) => new()
    {
        Id             = m.Id,
        UserId         = m.UserId,
        Provider       = (int)m.Provider,
        ProviderUserId = m.ProviderUserId,
        Email          = m.Email,
        AccessToken    = m.AccessToken,
        RefreshToken   = m.RefreshToken,
        ExpiresAt      = m.ExpiresAt,
        CreatedAt      = m.CreatedAt,
        UpdatedAt      = m.UpdatedAt
    };
}
