using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

/// <summary>
/// Domain interface for user data access in mobile app
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetCurrentUserAsync();
    Task SaveAsync(User user);
    Task<IEnumerable<User>> GetUnsyncedAsync();
    Task MarkAsSyncedAsync(Guid id);
}