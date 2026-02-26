using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
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
    private readonly IHealthDataSyncService _syncService;
    private readonly ILogger<AuthApplicationService> _logger;

    private AuthResultDto? _cachedUser;
    private OAuthTokenResult? _pendingTokens;

    private const string UserIdKey = "auth_user_id";

    public AuthApplicationService(
        IOAuthTokenProvider tokenProvider,
        ISecureTokenStorage tokenStorage,
        IUserRepository userRepo,
        IUserOAuthRepository oauthRepo,
        IPatientRepository patientRepo,
        IHealthDataSyncService syncService,
        ILogger<AuthApplicationService> logger)
    {
        _tokenProvider = tokenProvider;
        _tokenStorage  = tokenStorage;
        _userRepo      = userRepo;
        _oauthRepo     = oauthRepo;
        _patientRepo   = patientRepo;
        _syncService   = syncService;
        _logger        = logger;
    }

    public async Task<GoogleAuthCheckResult> AuthenticateWithGoogleAsync()
    {
        _logger.LogInformation("[Auth] Starting Google authentication...");

        var tokens = await _tokenProvider.GetTokensAsync();
        _logger.LogInformation("[Auth] Google tokens received. HasAccessToken={HasToken}", tokens.AccessToken is not null);

        var existingOAuth = await _oauthRepo.FindByProviderAndUserIdAsync(
            OAuthProvider.Google, tokens.ProviderUserId);

        if (existingOAuth is not null)
        {
            _logger.LogInformation("[Auth] Returning user. UserId={UserId}", existingOAuth.UserId);

            var user = (await _userRepo.GetByIdAsync(existingOAuth.UserId))!;

            existingOAuth.AccessToken  = tokens.AccessToken;
            existingOAuth.RefreshToken = tokens.RefreshToken;
            existingOAuth.ExpiresAt    = tokens.ExpiresAt;
            existingOAuth.Email        = tokens.Email;
            await _oauthRepo.UpdateAsync(existingOAuth);

            await SyncAuthToCloudAsync();

            await _tokenStorage.StoreAsync(UserIdKey, user.Id.ToString());

            var authResult = await BuildAuthResultAsync(user);
            _cachedUser = authResult;

            return new GoogleAuthCheckResult
            {
                IsExistingUser = true,
                AuthResult     = authResult,
                Email          = tokens.Email,
                FirstName      = tokens.FirstName,
                LastName       = tokens.LastName,
                PhotoUrl       = tokens.PhotoUrl
            };
        }

        _pendingTokens = tokens;
        _logger.LogInformation("[Auth] New user detected. Awaiting profile form.");

        return new GoogleAuthCheckResult
        {
            IsExistingUser = false,
            AuthResult     = null,
            Email          = tokens.Email,
            FirstName      = tokens.FirstName,
            LastName       = tokens.LastName,
            PhotoUrl       = tokens.PhotoUrl
        };
    }

    public async Task<AuthResultDto> CompleteRegistrationAsync(ProfileCompletionDto profile)
    {
        _logger.LogInformation("[Auth] CompleteRegistrationAsync called.");

        if (_pendingTokens is null)
            throw new InvalidOperationException("No pending Google authentication. Call AuthenticateWithGoogleAsync first.");

        var tokens = _pendingTokens;
        _pendingTokens = null;

        var user = new User
        {
            Email       = tokens.Email,
            Role        = UserRole.Patient,
            FirstName   = profile.FirstName,
            LastName    = profile.LastName,
            PhotoUrl    = tokens.PhotoUrl,
            Phone       = profile.Phone,
            Address     = profile.Address,
            City        = profile.City,
            Country     = profile.Country,
            DateOfBirth = profile.DateOfBirth
        };
        await _userRepo.AddAsync(user);
        _logger.LogInformation("[Auth] User created locally. UserId={UserId}", user.Id);

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
        _logger.LogInformation("[Auth] UserOAuth created locally. OAuthId={OAuthId}", oauth.Id);

        await SyncAuthToCloudAsync();

        await _tokenStorage.StoreAsync(UserIdKey, user.Id.ToString());

        var authResult = await BuildAuthResultAsync(user);
        _cachedUser = authResult;
        _logger.LogInformation("[Auth] Registration complete. DisplayName={Name}", authResult.DisplayName);
        return authResult;
    }

    public async Task<AuthResultDto> CreatePatientProfileAsync(PatientProfileDto profile)
    {
        _logger.LogInformation("[Auth] CreatePatientProfileAsync called.");

        var current = await GetCurrentUserAsync();
        if (current is null)
            throw new InvalidOperationException("No authenticated user. Sign in first.");

        var existingPatient = await _patientRepo.GetByUserIdAsync(current.UserId);
        if (existingPatient is not null)
        {
            _logger.LogWarning("[Auth] Patient profile already exists for UserId={UserId}. Updating.", current.UserId);
            existingPatient.BloodType = profile.BloodType;
            existingPatient.Allergies = profile.Allergies;
            existingPatient.MedicalHistoryNotes = profile.MedicalHistoryNotes;
            await _patientRepo.UpdateAsync(existingPatient);
        }
        else
        {
            var patient = new Patient
            {
                UserId              = current.UserId,
                BloodType           = profile.BloodType,
                Allergies           = profile.Allergies,
                MedicalHistoryNotes = profile.MedicalHistoryNotes
            };
            await _patientRepo.AddAsync(patient);
            _logger.LogInformation("[Auth] Patient profile created. PatientId={PatientId}", patient.Id);
        }

        await SyncAuthToCloudAsync();

        var user = (await _userRepo.GetByIdAsync(current.UserId))!;
        var authResult = await BuildAuthResultAsync(user);
        _cachedUser = authResult;
        return authResult;
    }

    public async Task<PatientDisplayDto?> GetPatientProfileAsync()
    {
        var current = await GetCurrentUserAsync();
        if (current is null) return null;
        var patient = await _patientRepo.GetByUserIdAsync(current.UserId);
        if (patient is null) return null;
        return new PatientDisplayDto
        {
            PatientId           = patient.Id,
            BloodType           = patient.BloodType,
            Allergies           = patient.Allergies,
            MedicalHistoryNotes = patient.MedicalHistoryNotes,
            CreatedAt           = patient.CreatedAt
        };
    }

    public async Task<AuthResultDto> SignInExistingUserAsync()
    {
        var current = await GetCurrentUserAsync();
        if (current is null)
            throw new InvalidOperationException("No authenticated user found.");
        return current;
    }

    public async Task SignOutAsync()
    {
        _cachedUser    = null;
        _pendingTokens = null;
        await _tokenStorage.ClearAllAsync();
        _logger.LogInformation("[Auth] Sign out complete.");
    }

    public async Task<AuthResultDto?> GetCurrentUserAsync()
    {
        if (_cachedUser is not null) return _cachedUser;

        var userIdStr = await _tokenStorage.GetAsync(UserIdKey);
        if (userIdStr is null || !long.TryParse(userIdStr, out var userId)) return null;

        var user = await _userRepo.GetByIdAsync(userId);
        if (user is null)
        {
            await _tokenStorage.ClearAllAsync();
            return null;
        }

        _cachedUser = await BuildAuthResultAsync(user);
        return _cachedUser;
    }

    public async Task<long?> GetCurrentPatientIdAsync()
    {
        var current = await GetCurrentUserAsync();
        if (current is null) return null;
        var patient = await _patientRepo.GetByUserIdAsync(current.UserId);
        return patient?.Id;
    }

    private async Task<AuthResultDto> BuildAuthResultAsync(User user)
    {
        var patient = await _patientRepo.GetByUserIdAsync(user.Id);
        return new AuthResultDto
        {
            UserId            = user.Id,
            Email             = user.Email,
            DisplayName       = $"{user.FirstName} {user.LastName}".Trim(),
            PhotoUrl          = user.PhotoUrl,
            HasPatientProfile = patient is not null
        };
    }

    /// <summary>
    /// Triggers a full cloud drain covering User, UserOAuth, Patient (and all other
    /// registered <see cref="ITableDrainer"/> implementations). Called immediately
    /// after login/registration/profile creation so data reaches the cloud without
    /// waiting for the background drain timer.
    /// </summary>
    private async Task SyncAuthToCloudAsync()
    {
        _logger.LogInformation("[Auth] Triggering cloud sync for auth entities...");
        try
        {
            await _syncService.PushToCloudAsync();
            _logger.LogInformation("[Auth] Cloud sync complete.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Auth] Cloud sync failed. App continues offline; records will retry on next drain cycle.");
        }
    }
}
