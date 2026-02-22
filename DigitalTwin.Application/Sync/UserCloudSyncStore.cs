using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalTwin.Application.Sync;

public class UserCloudSyncStore : ICloudSyncStore<User>
{
    private readonly IServiceProvider _sp;

    public UserCloudSyncStore(IServiceProvider sp) => _sp = sp;

    private IUserRepository CloudRepo =>
        _sp.GetKeyedService<IUserRepository>("Cloud") ?? throw new InvalidOperationException("Cloud User repository not registered.");

    public async Task AddAsync(User item)
    {
        if (await CloudRepo.ExistsAsync(item))
        {
            var existing = await CloudRepo.GetByIdAsync(item.Id) ?? await CloudRepo.GetByEmailAsync(item.Email);
            if (existing is not null)
            {
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
                return;
            }
        }
        await CloudRepo.AddAsync(item);
    }

    public async Task<bool> ExistsAsync(User item)
    {
        return await CloudRepo.ExistsAsync(item);
    }
}
