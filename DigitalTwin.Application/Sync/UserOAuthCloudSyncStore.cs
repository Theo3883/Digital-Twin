using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync;

public class UserOAuthCloudSyncStore : ICloudSyncStore<UserOAuth>
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<UserOAuthCloudSyncStore> _logger;

    public UserOAuthCloudSyncStore(IServiceProvider sp, ILogger<UserOAuthCloudSyncStore> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    private IUserOAuthRepository CloudRepo =>
        _sp.GetKeyedService<IUserOAuthRepository>("Cloud") ?? throw new InvalidOperationException("Cloud UserOAuth repository not registered.");

    public async Task AddAsync(UserOAuth item)
    {
        _logger.LogInformation("[CloudSync:OAuth] AddAsync called. UserId={UserId}, Provider={Provider}",
            item.UserId, item.Provider);
        _logger.LogDebug("[CloudSync:OAuth] ProviderUserId={Sub}", item.ProviderUserId);

        var existing = await CloudRepo.FindByProviderAndUserIdAsync(item.Provider, item.ProviderUserId);
        if (existing is not null)
        {
            _logger.LogInformation("[CloudSync:OAuth] Existing OAuth found in cloud (Id={Id}). Updating.", existing.Id);
            existing.UserId = item.UserId;
            existing.Email = item.Email;
            existing.AccessToken = item.AccessToken;
            existing.RefreshToken = item.RefreshToken;
            existing.ExpiresAt = item.ExpiresAt;
            await CloudRepo.UpdateAsync(existing);
            await CloudRepo.MarkSyncedAsync([existing]);
            _logger.LogInformation("[CloudSync:OAuth] Updated and marked synced in cloud.");
            return;
        }

        _logger.LogInformation("[CloudSync:OAuth] Inserting new OAuth into cloud.");
        await CloudRepo.AddAsync(item);
        await CloudRepo.MarkSyncedAsync([item]);
        _logger.LogInformation("[CloudSync:OAuth] Inserted and marked synced in cloud.");
    }

    public async Task<bool> ExistsAsync(UserOAuth item)
    {
        var exists = await CloudRepo.ExistsAsync(item);
        _logger.LogDebug("[CloudSync:OAuth] ExistsAsync -> {Exists}", exists);
        return exists;
    }
}
