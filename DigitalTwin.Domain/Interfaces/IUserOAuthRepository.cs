using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface IUserOAuthRepository
{
    Task<UserOAuth?> FindByProviderAndUserIdAsync(OAuthProvider provider, string providerUserId);
    Task AddAsync(UserOAuth userOAuth);
    Task UpdateAsync(UserOAuth userOAuth);
}
