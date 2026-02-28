using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface IUserService
{
    Task<User> CreateUserAsync(
        OAuthTokenResult tokens,
        string firstName, string lastName, string? phone,
        string? address, string? city, string? country, DateTime? dateOfBirth);
    Task<User?> GetByIdAsync(Guid userId);
}
