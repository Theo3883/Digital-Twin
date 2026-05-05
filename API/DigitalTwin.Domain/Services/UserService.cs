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
    public async Task<User> CreateUserAsync(CreateUserRequest request)
    {
        var user = new User
        {
            Email       = request.Tokens.Email,
            Role        = UserRole.Patient,
            FirstName   = request.FirstName,
            LastName    = request.LastName,
            PhotoUrl    = request.Tokens.PhotoUrl,
            Phone       = request.Phone,
            Address     = request.Address,
            City        = request.City,
            Country     = request.Country,
            DateOfBirth = request.DateOfBirth
        };
        await _userRepo.AddAsync(user);

        var oauth = new UserOAuth
        {
            UserId         = user.Id,
            Provider       = OAuthProvider.Google,
            ProviderUserId = request.Tokens.ProviderUserId,
            Email          = request.Tokens.Email,
            AccessToken    = request.Tokens.AccessToken,
            RefreshToken   = request.Tokens.RefreshToken,
            ExpiresAt      = request.Tokens.ExpiresAt
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
