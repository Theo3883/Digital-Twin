using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalTwin.Application.Sync;

public class UserOAuthCloudSyncStore : ICloudSyncStore<UserOAuth>
{
    private readonly IServiceProvider _sp;

    public UserOAuthCloudSyncStore(IServiceProvider sp) => _sp = sp;

    private IUserOAuthRepository CloudRepo =>
        _sp.GetKeyedService<IUserOAuthRepository>("Cloud") ?? throw new InvalidOperationException("Cloud UserOAuth repository not registered.");

    public async Task AddAsync(UserOAuth item)
    {
        var existing = await CloudRepo.FindByProviderAndUserIdAsync(item.Provider, item.ProviderUserId);
        if (existing is not null)
        {
            existing.UserId = item.UserId;
            existing.Email = item.Email;
            existing.AccessToken = item.AccessToken;
            existing.RefreshToken = item.RefreshToken;
            existing.ExpiresAt = item.ExpiresAt;
            await CloudRepo.UpdateAsync(existing);
            return;
        }
        await CloudRepo.AddAsync(item);
    }

    public async Task<bool> ExistsAsync(UserOAuth item)
    {
        return await CloudRepo.ExistsAsync(item);
    }
}
