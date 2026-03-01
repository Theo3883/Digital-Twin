using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Thin application layer service for auth. Delegates all business logic to
/// <see cref="IAuthService"/> and <see cref="IPatientService"/> in the Domain layer.
/// Responsible only for DTO mapping, validation, caching, and sync triggering.
/// </summary>
public class AuthApplicationService : IAuthApplicationService
{
    private readonly IAuthService _authService;
    private readonly IPatientService _patientService;
    private readonly IHealthDataSyncService _syncService;
    private readonly ILogger<AuthApplicationService> _logger;

    private AuthResultDto? _cachedUser;

    public AuthApplicationService(
        IAuthService authService,
        IPatientService patientService,
        IHealthDataSyncService syncService,
        ILogger<AuthApplicationService> logger)
    {
        _authService    = authService;
        _patientService = patientService;
        _syncService    = syncService;
        _logger         = logger;
    }

    public async Task<GoogleAuthCheckResult> AuthenticateWithGoogleAsync()
    {
        _logger.LogInformation("[Auth] Starting Google authentication...");

        var result = await _authService.AuthenticateWithGoogleAsync();

        if (result.IsExistingUser)
        {
            _logger.LogDebug("[Auth] Returning user. UserId={UserId}", result.User!.Id);
            await TriggerCloudSync();
            var authResult = await BuildAuthResultAsync(result.User!);
            _cachedUser = authResult;

            return new GoogleAuthCheckResult
            {
                IsExistingUser = true,
                AuthResult     = authResult,
                Email          = result.Email,
                FirstName      = result.FirstName,
                LastName        = result.LastName,
                PhotoUrl       = result.PhotoUrl
            };
        }

        _logger.LogDebug("[Auth] New user detected. Awaiting profile form.");

        return new GoogleAuthCheckResult
        {
            IsExistingUser = false,
            AuthResult     = null,
            Email          = result.Email,
            FirstName      = result.FirstName,
            LastName        = result.LastName,
            PhotoUrl       = result.PhotoUrl
        };
    }

    public async Task<AuthResultDto> CompleteRegistrationAsync(ProfileCompletionDto profile)
    {
        _logger.LogInformation("[Auth] CompleteRegistrationAsync called.");

        var user = await _authService.RegisterUserAsync(
            profile.FirstName, profile.LastName, profile.Phone,
            profile.Address, profile.City, profile.Country, profile.DateOfBirth);

        _logger.LogDebug("[Auth] User created. UserId={UserId}", user.Id);

        await TriggerCloudSync();

        var authResult = await BuildAuthResultAsync(user);
        _cachedUser = authResult;
        _logger.LogDebug("[Auth] Registration complete. DisplayName={Name}", authResult.DisplayName);
        return authResult;
    }

    public async Task<AuthResultDto> CreatePatientProfileAsync(PatientProfileDto profile)
    {
        _logger.LogInformation("[Auth] CreatePatientProfileAsync called.");

        var current = await GetCurrentUserAsync();
        if (current is null)
            throw new InvalidOperationException("No authenticated user. Sign in first.");

        await _patientService.CreateOrUpdateProfileAsync(
            current.UserId, profile.BloodType, profile.Allergies, profile.MedicalHistoryNotes);

        _logger.LogDebug("[Auth] Patient profile saved for UserId={UserId}.", current.UserId);

        await TriggerCloudSync();

        var user = (await _authService.GetCurrentUserAsync())!;
        var authResult = await BuildAuthResultAsync(user);
        _cachedUser = authResult;
        return authResult;
    }

    public async Task<PatientDisplayDto?> GetPatientProfileAsync()
    {
        var current = await GetCurrentUserAsync();
        if (current is null) return null;

        var patient = await _patientService.GetByUserIdAsync(current.UserId);
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
        _cachedUser = null;
        await _authService.SignOutAsync();
        _logger.LogInformation("[Auth] Sign out complete.");
    }

    public async Task<AuthResultDto?> GetCurrentUserAsync()
    {
        if (_cachedUser is not null) return _cachedUser;

        var user = await _authService.GetCurrentUserAsync();
        if (user is null) return null;

        _cachedUser = await BuildAuthResultAsync(user);
        return _cachedUser;
    }

    public async Task<Guid?> GetCurrentPatientIdAsync()
    {
        var user = await _authService.GetCurrentUserAsync();
        if (user is null) return null;
        return await _patientService.GetPatientIdForUserAsync(user.Id);
    }

    private async Task<AuthResultDto> BuildAuthResultAsync(Domain.Models.User user)
    {
        var hasProfile = await _patientService.HasPatientProfileAsync(user.Id);
        return new AuthResultDto
        {
            UserId            = user.Id,
            Email             = user.Email,
            DisplayName       = $"{user.FirstName} {user.LastName}".Trim(),
            PhotoUrl          = user.PhotoUrl,
            HasPatientProfile = hasProfile
        };
    }

    private async Task TriggerCloudSync()
    {
        _logger.LogDebug("[Auth] Triggering cloud sync for auth entities...");
        try
        {
            await _syncService.PushToCloudAsync();
            _logger.LogDebug("[Auth] Cloud sync complete.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Auth] Cloud sync failed. Records will retry on next drain cycle.");
        }
    }
}
