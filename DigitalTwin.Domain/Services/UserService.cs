using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Services;

/// <summary>
/// Domain service for user lifecycle: creation and lookup.
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;
    private readonly IUserOAuthRepository _oauthRepo;

    public UserService(IUserRepository userRepo, IUserOAuthRepository oauthRepo)
    {
        _userRepo  = userRepo;
        _oauthRepo = oauthRepo;
    }

    /// <summary>
    /// Creates a new User + UserOAuth from OAuth token data and profile fields.
    /// </summary>
    public async Task<User> CreateUserAsync(
        OAuthTokenResult tokens,
        string firstName, string lastName, string? phone,
        string? address, string? city, string? country, DateTime? dateOfBirth)
    {
        var user = new User
        {
            Email       = tokens.Email,
            Role        = UserRole.Patient,
            FirstName   = firstName,
            LastName    = lastName,
            PhotoUrl    = tokens.PhotoUrl,
            Phone       = phone,
            Address     = address,
            City        = city,
            Country     = country,
            DateOfBirth = dateOfBirth
        };
        await _userRepo.AddAsync(user);

        var oauth = new UserOAuth
        {
            UserId         = user.Id,
            Provider       = OAuthProvider.Google,
            ProviderUserId = tokens.ProviderUserId,
            Email          = tokens.Email,
            AccessToken    = tokens.AccessToken,
            RefreshToken   = tokens.RefreshToken,
            ExpiresAt      = tokens.ExpiresAt
        };
        await _oauthRepo.AddAsync(oauth);

        return user;
    }

    /// <summary>
    /// Returns a user by ID, or null if not found.
    /// </summary>
    public async Task<User?> GetByIdAsync(Guid userId)
    {
        return await _userRepo.GetByIdAsync(userId);
    }
}
