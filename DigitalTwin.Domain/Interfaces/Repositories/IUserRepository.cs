using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
    Task<IEnumerable<User>> GetDirtyAsync();
    Task MarkSyncedAsync(IEnumerable<User> items);
    Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc);
    Task<bool> ExistsAsync(User user);
}
