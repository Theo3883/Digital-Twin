using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Sync;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

public class AuthApplicationService : IAuthApplicationService
{
    private readonly IOAuthTokenProvider _tokenProvider;
    private readonly ISecureTokenStorage _tokenStorage;
    private readonly IUserRepository _userRepo;
    private readonly IUserOAuthRepository _oauthRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly ISyncFacade<User> _userSync;
    private readonly ISyncFacade<Patient> _patientSync;
    private readonly ISyncFacade<UserOAuth> _oauthSync;
    private readonly ILogger<AuthApplicationService> _logger;

    private AuthResultDto? _cachedUser;

    /// <summary>
    /// Holds the Google tokens between AuthenticateWithGoogleAsync and CompleteRegistrationAsync.
    /// </summary>
    private OAuthTokenResult? _pendingTokens;

    private const string UserIdKey = "auth_user_id";
    private const string PatientIdKey = "auth_patient_id";

    public AuthApplicationService(
        IOAuthTokenProvider tokenProvider,
        ISecureTokenStorage tokenStorage,
        IUserRepository userRepo,
        IUserOAuthRepository oauthRepo,
        IPatientRepository patientRepo,
        ISyncFacade<User> userSync,
        ISyncFacade<Patient> patientSync,
        ISyncFacade<UserOAuth> oauthSync,
        ILogger<AuthApplicationService> logger)
    {
        _tokenProvider = tokenProvider;
        _tokenStorage = tokenStorage;
        _userRepo = userRepo;
        _oauthRepo = oauthRepo;
        _patientRepo = patientRepo;
        _userSync = userSync;
        _patientSync = patientSync;
        _oauthSync = oauthSync;
        _logger = logger;
    }

    public async Task<GoogleAuthCheckResult> AuthenticateWithGoogleAsync()
    {
        _logger.LogInformation("[Auth] Starting Google authentication...");

        var tokens = await _tokenProvider.GetTokensAsync();
        _logger.LogInformation("[Auth] Google tokens received. HasAccessToken={HasToken}", tokens.AccessToken is not null);
        _logger.LogDebug("[Auth] Token identity: Email={Email}, ProviderUserId={Sub}, FirstName={First}, LastName={Last}",
            tokens.Email, tokens.ProviderUserId, tokens.FirstName, tokens.LastName);

        // Check if this Google account is already linked
        var existingOAuth = await _oauthRepo.FindByProviderAndUserIdAsync(
            OAuthProvider.Google, tokens.ProviderUserId);

        if (existingOAuth is not null)
        {
            _logger.LogInformation("[Auth] Existing OAuth found. Signing in returning user. UserId={UserId}", existingOAuth.UserId);
            _logger.LogDebug("[Auth] Returning user ProviderUserId={Sub}", tokens.ProviderUserId);

            // Returning user → update tokens and sign in directly
            var user = (await _userRepo.GetByIdAsync(existingOAuth.UserId))!;
            var patient = (await _patientRepo.GetByUserIdAsync(user.Id))!;

            _logger.LogInformation("[Auth] Loaded user. UserId={UserId}", user.Id);
            _logger.LogDebug("[Auth] User identity: Email={Email}, FirstName={First}, LastName={Last}",
                user.Email, user.FirstName, user.LastName);

            existingOAuth.AccessToken = tokens.AccessToken;
            existingOAuth.RefreshToken = tokens.RefreshToken;
            existingOAuth.ExpiresAt = tokens.ExpiresAt;
            existingOAuth.Email = tokens.Email;
            await _oauthRepo.UpdateAsync(existingOAuth);
            _logger.LogInformation("[Auth] Updated OAuth tokens for existing user.");

            await SyncAuthToCloudAsync();

            await _tokenStorage.StoreAsync(UserIdKey, user.Id.ToString());
            await _tokenStorage.StoreAsync(PatientIdKey, patient.Id.ToString());
            _logger.LogInformation("[Auth] Stored user/patient IDs in secure storage. UserId={UserId}, PatientId={PatientId}",
                user.Id, patient.Id);

            var authResult = BuildAuthResult(user, patient);
            _cachedUser = authResult;

            return new GoogleAuthCheckResult
            {
                IsExistingUser = true,
                AuthResult = authResult,
                Email = tokens.Email,
                FirstName = tokens.FirstName,
                LastName = tokens.LastName,
                PhotoUrl = tokens.PhotoUrl
            };
        }

        // New user → store tokens temporarily, do NOT create DB records yet
        _pendingTokens = tokens;
        _logger.LogInformation("[Auth] New user detected. Awaiting profile form.");
        _logger.LogDebug("[Auth] Pending registration for Email={Email}", tokens.Email);

        return new GoogleAuthCheckResult
        {
            IsExistingUser = false,
            AuthResult = null,
            Email = tokens.Email,
            FirstName = tokens.FirstName,
            LastName = tokens.LastName,
            PhotoUrl = tokens.PhotoUrl
        };
    }

    public async Task<AuthResultDto> CompleteRegistrationAsync(ProfileCompletionDto profile)
    {
        _logger.LogInformation("[Auth] CompleteRegistrationAsync called.");
        _logger.LogDebug("[Auth] Registration profile: FirstName={First}, LastName={Last}, Phone={Phone}, City={City}, Country={Country}, DOB={DOB}",
            profile.FirstName, profile.LastName, profile.Phone, profile.City, profile.Country, profile.DateOfBirth);

        if (_pendingTokens is null)
        {
            _logger.LogError("[Auth] CompleteRegistrationAsync called but _pendingTokens is null.");
            throw new InvalidOperationException("No pending Google authentication. Call AuthenticateWithGoogleAsync first.");
        }

        var tokens = _pendingTokens;
        _pendingTokens = null;

        // Create user with form data (FirstName/LastName come from the form, which may have been edited)
        var user = new User
        {
            Email = tokens.Email,
            Role = UserRole.Patient,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            PhotoUrl = tokens.PhotoUrl,
            Phone = profile.Phone,
            Address = profile.Address,
            City = profile.City,
            Country = profile.Country,
            DateOfBirth = profile.DateOfBirth
        };
        await _userRepo.AddAsync(user);
        _logger.LogInformation("[Auth] User created in local DB. UserId={UserId}", user.Id);
        _logger.LogDebug("[Auth] New user identity: Email={Email}, FirstName={First}, LastName={Last}",
            user.Email, user.FirstName, user.LastName);

        var patient = new Patient { UserId = user.Id };
        await _patientRepo.AddAsync(patient);
        _logger.LogInformation("[Auth] Patient created in local DB. PatientId={PatientId}, UserId={UserId}", patient.Id, user.Id);

        var oauth = new UserOAuth
        {
            UserId = user.Id,
            Provider = OAuthProvider.Google,
            ProviderUserId = tokens.ProviderUserId,
            Email = tokens.Email,
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            ExpiresAt = tokens.ExpiresAt
        };
        await _oauthRepo.AddAsync(oauth);
        _logger.LogInformation("[Auth] UserOAuth created in local DB. OAuthId={OAuthId}", oauth.Id);
        _logger.LogDebug("[Auth] OAuth ProviderUserId={Sub}", oauth.ProviderUserId);

        await SyncAuthToCloudAsync();

        await _tokenStorage.StoreAsync(UserIdKey, user.Id.ToString());
        await _tokenStorage.StoreAsync(PatientIdKey, patient.Id.ToString());
        _logger.LogInformation("[Auth] Stored user/patient IDs in secure storage. UserId={UserId}, PatientId={PatientId}",
            user.Id, patient.Id);

        var authResult = BuildAuthResult(user, patient);
        _cachedUser = authResult;
        _logger.LogInformation("[Auth] Registration complete. DisplayName={Name}", authResult.DisplayName);
        return authResult;
    }

    public async Task<AuthResultDto> SignInExistingUserAsync()
    {
        _logger.LogInformation("[Auth] SignInExistingUserAsync called.");
        var current = await GetCurrentUserAsync();
        if (current is null)
        {
            _logger.LogWarning("[Auth] SignInExistingUserAsync: No authenticated user found.");
            throw new InvalidOperationException("No authenticated user found.");
        }
        return current;
    }

    public async Task SignOutAsync()
    {
        _logger.LogInformation("[Auth] SignOutAsync called. Clearing cached user and secure storage.");
        _cachedUser = null;
        _pendingTokens = null;
        await _tokenStorage.ClearAllAsync();
        _logger.LogInformation("[Auth] Sign out complete.");
    }

    public async Task<AuthResultDto?> GetCurrentUserAsync()
    {
        if (_cachedUser is not null)
        {
            _logger.LogDebug("[Auth] GetCurrentUserAsync returning cached user. UserId={UserId}", _cachedUser.UserId);
            return _cachedUser;
        }

        var userIdStr = await _tokenStorage.GetAsync(UserIdKey);
        _logger.LogDebug("[Auth] GetCurrentUserAsync: SecureStorage UserId={StoredId}", userIdStr ?? "null");

        if (userIdStr is null || !long.TryParse(userIdStr, out var userId))
        {
            _logger.LogDebug("[Auth] GetCurrentUserAsync: No valid UserId in secure storage. User not logged in.");
            return null;
        }

        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null)
        {
            _logger.LogWarning("[Auth] GetCurrentUserAsync: UserId={UserId} found in secure storage but NOT in local DB. Clearing stale session.", userId);
            await _tokenStorage.ClearAllAsync();
            return null;
        }

        var patient = await _patientRepo.GetByUserIdAsync(userId);
        if (patient is null)
        {
            _logger.LogWarning("[Auth] GetCurrentUserAsync: Patient not found for UserId={UserId}.", userId);
            return null;
        }

        _cachedUser = BuildAuthResult(user, patient);
        _logger.LogInformation("[Auth] GetCurrentUserAsync: Loaded from DB. UserId={UserId}", user.Id);
        _logger.LogDebug("[Auth] Cached user: Email={Email}, DisplayName={Name}", user.Email, _cachedUser.DisplayName);
        return _cachedUser;
    }

    public async Task<long?> GetCurrentPatientIdAsync()
    {
        var current = await GetCurrentUserAsync();
        return current?.PatientId;
    }

    private static AuthResultDto BuildAuthResult(User user, Patient patient) => new()
    {
        UserId = user.Id,
        PatientId = patient.Id,
        Email = user.Email,
        DisplayName = $"{user.FirstName} {user.LastName}".Trim(),
        PhotoUrl = user.PhotoUrl
    };

    private async Task SyncAuthToCloudAsync()
    {
        _logger.LogInformation("[Auth] Starting cloud sync for auth data...");
        try
        {
            var purgeOlderThan = TimeSpan.FromDays(7);

            _logger.LogInformation("[Auth] Syncing Users to cloud...");
            await _userSync.SyncAsync(purgeOlderThan);
            _logger.LogInformation("[Auth] Users synced.");

            _logger.LogInformation("[Auth] Syncing Patients to cloud...");
            await _patientSync.SyncAsync(purgeOlderThan);
            _logger.LogInformation("[Auth] Patients synced.");

            _logger.LogInformation("[Auth] Syncing UserOAuth to cloud...");
            await _oauthSync.SyncAsync(purgeOlderThan);
            _logger.LogInformation("[Auth] UserOAuth synced.");

            _logger.LogInformation("[Auth] Cloud sync complete.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Auth] Cloud sync failed. App continues offline.");
        }
    }
}
