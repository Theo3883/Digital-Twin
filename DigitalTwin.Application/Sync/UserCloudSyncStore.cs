using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync;

public class UserCloudSyncStore : ICloudSyncStore<User>
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<UserCloudSyncStore> _logger;

    public UserCloudSyncStore(IServiceProvider sp, ILogger<UserCloudSyncStore> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    private IUserRepository CloudRepo =>
        _sp.GetKeyedService<IUserRepository>("Cloud") ?? throw new InvalidOperationException("Cloud User repository not registered.");

    public async Task AddAsync(User item)
    {
        _logger.LogInformation("[CloudSync:User] AddAsync called. Id={Id}, Email={Email}", item.Id, item.Email);

        if (await CloudRepo.ExistsAsync(item))
        {
            var existing = await CloudRepo.GetByIdAsync(item.Id) ?? await CloudRepo.GetByEmailAsync(item.Email);
            if (existing is not null)
            {
                _logger.LogInformation("[CloudSync:User] Existing user found in cloud (Id={CloudId}). Updating.", existing.Id);
                existing.Email = item.Email;
                existing.Role = item.Role;
                existing.FirstName = item.FirstName;
                existing.LastName = item.LastName;
                existing.PhotoUrl = item.PhotoUrl;
                existing.Phone = item.Phone;
                existing.Address = item.Address;
                existing.City = item.City;
                existing.Country = item.Country;
                existing.DateOfBirth = item.DateOfBirth;
                await CloudRepo.UpdateAsync(existing);
                await CloudRepo.MarkSyncedAsync([existing]);
                _logger.LogInformation("[CloudSync:User] Updated and marked synced in cloud.");
                return;
            }
        }

        _logger.LogInformation("[CloudSync:User] Inserting new user into cloud. Email={Email}", item.Email);
        await CloudRepo.AddAsync(item);
        await CloudRepo.MarkSyncedAsync([item]);
        _logger.LogInformation("[CloudSync:User] Inserted and marked synced in cloud.");
    }

    public async Task<bool> ExistsAsync(User item)
    {
        var exists = await CloudRepo.ExistsAsync(item);
        _logger.LogDebug("[CloudSync:User] ExistsAsync Email={Email} -> {Exists}", item.Email, exists);
        return exists;
    }
}
