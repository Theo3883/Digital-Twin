using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Services;

/// <summary>
/// Domain service for OAuth authentication flow and session management.
/// Owns: Google OAuth lookup, token refresh, pending-token state, session resolution, sign-out.
/// Delegates user CRUD to <see cref="IUserService"/>.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IOAuthTokenProvider _tokenProvider;
    private readonly ISecureTokenStorage _tokenStorage;
    private readonly IUserOAuthRepository _oauthRepo;
    private readonly IUserService _userService;

    private OAuthTokenResult? _pendingTokens;

    private const string UserIdKey = "auth_user_id";

    public AuthService(
        IOAuthTokenProvider tokenProvider,
        ISecureTokenStorage tokenStorage,
        IUserOAuthRepository oauthRepo,
        IUserService userService)
    {
        _tokenProvider = tokenProvider;
        _tokenStorage  = tokenStorage;
        _oauthRepo     = oauthRepo;
        _userService   = userService;
    }

    /// <summary>
    /// Authenticates via Google and checks if the user already exists.
    /// For returning users, updates OAuth tokens. For new users, stashes tokens
    /// so <see cref="RegisterUserAsync"/> can complete the flow.
    /// </summary>
    public async Task<GoogleAuthCheckResult> AuthenticateWithGoogleAsync()
    {
        var tokens = await _tokenProvider.GetTokensAsync();

        var existingOAuth = await _oauthRepo.FindByProviderAndUserIdAsync(
            OAuthProvider.Google, tokens.ProviderUserId);

        if (existingOAuth is not null)
        {
            var user = (await _userService.GetByIdAsync(existingOAuth.UserId))!;

            existingOAuth.AccessToken  = tokens.AccessToken;
            existingOAuth.RefreshToken = tokens.RefreshToken;
            existingOAuth.ExpiresAt    = tokens.ExpiresAt;
            existingOAuth.Email        = tokens.Email;
            await _oauthRepo.UpdateAsync(existingOAuth);

            await _tokenStorage.StoreAsync(UserIdKey, user.Id.ToString());

            return new GoogleAuthCheckResult
            {
                IsExistingUser = true,
                User           = user,
                Email          = tokens.Email,
                FirstName      = tokens.FirstName,
                LastName       = tokens.LastName,
                PhotoUrl       = tokens.PhotoUrl
            };
        }

        _pendingTokens = tokens;

        return new GoogleAuthCheckResult
        {
            IsExistingUser = false,
            User           = null,
            Email          = tokens.Email,
            FirstName      = tokens.FirstName,
            LastName       = tokens.LastName,
            PhotoUrl       = tokens.PhotoUrl
        };
    }

    /// <summary>
    /// Completes the OAuth registration flow: consumes pending tokens,
    /// delegates user creation to <see cref="IUserService"/>, and stores the session.
    /// </summary>
    public async Task<User> RegisterUserAsync(
        string firstName, string lastName, string? phone,
        string? address, string? city, string? country, DateTime? dateOfBirth)
    {
        if (_pendingTokens is null)
            throw new InvalidOperationException("No pending Google authentication. Call AuthenticateWithGoogleAsync first.");

        var tokens = _pendingTokens;
        _pendingTokens = null;

        var user = await _userService.CreateUserAsync(
            tokens, firstName, lastName, phone, address, city, country, dateOfBirth);

        await _tokenStorage.StoreAsync(UserIdKey, user.Id.ToString());

        return user;
    }

    /// <summary>
    /// Resolves the currently authenticated user from secure storage, or null if not signed in.
    /// </summary>
    public async Task<User?> GetCurrentUserAsync()
    {
        var userIdStr = await _tokenStorage.GetAsync(UserIdKey);
        if (userIdStr is null || !long.TryParse(userIdStr, out var userId)) return null;

        var user = await _userService.GetByIdAsync(userId);
        if (user is null)
        {
            await _tokenStorage.ClearAllAsync();
            return null;
        }

        return user;
    }

    /// <summary>
    /// Clears authentication state (pending tokens and stored session).
    /// </summary>
    public async Task SignOutAsync()
    {
        _pendingTokens = null;
        await _tokenStorage.ClearAllAsync();
    }
}
