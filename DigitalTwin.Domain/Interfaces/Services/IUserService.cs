using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public record CreateUserRequest(
    OAuthTokenResult Tokens,
    string FirstName,
    string LastName,
    string? Phone,
    string? Address,
    string? City,
    string? Country,
    DateTime? DateOfBirth);

public interface IUserService
{
    Task<User> CreateUserAsync(CreateUserRequest request);
    Task<User?> GetByIdAsync(Guid userId);
}
