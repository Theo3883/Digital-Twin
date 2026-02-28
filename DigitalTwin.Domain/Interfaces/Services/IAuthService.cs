using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface IAuthService
{
    Task<GoogleAuthCheckResult> AuthenticateWithGoogleAsync();
    Task<User> RegisterUserAsync(
        string firstName, string lastName, string? phone,
        string? address, string? city, string? country, DateTime? dateOfBirth);
    Task<User?> GetCurrentUserAsync();
    Task SignOutAsync();
}
