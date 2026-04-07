using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Exceptions;
using DigitalTwin.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Orchestrates authentication, onboarding, and patient profile application workflows.
/// </summary>
public class AuthApplicationService : IAuthApplicationService
{
    private readonly IAuthService _authService;
    private readonly IPatientService _patientService;
    private readonly IHealthDataSyncService _syncService;
    private readonly ILogger<AuthApplicationService> _logger;

    private AuthResultDto? _cachedUser;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthApplicationService"/> class.
    /// </summary>
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

    /// <summary>
    /// Starts Google authentication and returns onboarding state for the authenticated user.
    /// </summary>
    public async Task<GoogleAuthCheckResult> AuthenticateWithGoogleAsync()
    {
        _logger.LogInformation("[Auth] Starting Google authentication...");

        var result = await _authService.AuthenticateWithGoogleAsync();

        if (result.IsExistingUser)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("[Auth] Returning user. UserId={UserId}", result.User!.Id);
            TriggerCloudSync();
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

    /// <summary>
    /// Completes registration for a newly authenticated user.
    /// </summary>
    public async Task<AuthResultDto> CompleteRegistrationAsync(ProfileCompletionDto profile)
    {
        _logger.LogInformation("[Auth] CompleteRegistrationAsync called.");

        var user = await _authService.RegisterUserAsync(
            profile.FirstName, profile.LastName, profile.Phone,
            profile.Address, profile.City, profile.Country, profile.DateOfBirth);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[Auth] User created. UserId={UserId}", user.Id);

        TriggerCloudSync();

        var authResult = await BuildAuthResultAsync(user);
        _cachedUser = authResult;
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[Auth] Registration complete. DisplayName={Name}", authResult.DisplayName);
        return authResult;
    }

    /// <summary>
    /// Creates or updates the current user's patient profile.
    /// </summary>
    public async Task<AuthResultDto> CreatePatientProfileAsync(PatientProfileDto profile)
    {
        _logger.LogInformation("[Auth] CreatePatientProfileAsync called.");

        var current = await GetCurrentUserAsync();
        if (current is null)
            throw new UnauthorizedException("No authenticated user. Sign in first.");

        var user = await _authService.GetCurrentUserAsync();

        await _patientService.CreateOrUpdateProfileAsync(
            current.UserId,
            new Domain.Models.PatientProfileUpdate(
                profile.BloodType,
                profile.Allergies,
                profile.MedicalHistoryNotes,
                profile.Weight,
                profile.Height,
                profile.BloodPressureSystolic,
                profile.BloodPressureDiastolic,
                profile.Cholesterol,
                profile.Cnp,
                user?.DateOfBirth));

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[Auth] Patient profile saved for UserId={UserId}.", current.UserId);

        TriggerCloudSync();

        user = (await _authService.GetCurrentUserAsync())!;
        var authResult = await BuildAuthResultAsync(user);
        _cachedUser = authResult;
        return authResult;
    }

    /// <summary>
    /// Gets the current user's patient profile for display.
    /// </summary>
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
            Weight              = patient.Weight,
            Height              = patient.Height,
            BloodPressureSystolic  = patient.BloodPressureSystolic,
            BloodPressureDiastolic = patient.BloodPressureDiastolic,
            Cholesterol         = patient.Cholesterol,
            Cnp                 = patient.Cnp,
            CreatedAt           = patient.CreatedAt
        };
    }

    /// <summary>
    /// Returns the current signed-in user or throws when none is available.
    /// </summary>
    public async Task<AuthResultDto> SignInExistingUserAsync()
    {
        var current = await GetCurrentUserAsync();
        if (current is null)
            throw new UnauthorizedException("No authenticated user found.");
        return current;
    }

    /// <summary>
    /// Signs out the current user and clears the cached authentication result.
    /// </summary>
    public async Task SignOutAsync()
    {
        _cachedUser = null;
        await _authService.SignOutAsync();
        _logger.LogInformation("[Auth] Sign out complete.");
    }

    /// <summary>
    /// Gets the current authenticated user, using the cached result when available.
    /// </summary>
    public async Task<AuthResultDto?> GetCurrentUserAsync()
    {
        if (_cachedUser is not null) return _cachedUser;

        var user = await _authService.GetCurrentUserAsync();
        if (user is null) return null;

        _cachedUser = await BuildAuthResultAsync(user);
        return _cachedUser;
    }

    /// <summary>
    /// Gets the patient identifier associated with the current authenticated user.
    /// </summary>
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
            HasPatientProfile = hasProfile,
            DateOfBirth       = user.DateOfBirth
        };
    }

    private void TriggerCloudSync()
    {
        // Fire-and-forget: cloud sync must not block sign-in/sign-up navigation.
        // If the cloud DB is unreachable the Npgsql connection will time out quickly
        // (Timeout=5 in the connection string) and the drain timer will retry later.
        _ = Task.Run(async () =>
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("[Auth] Triggering cloud sync for auth entities...");
            try
            {
                await _syncService.PushToCloudAsync();
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("[Auth] Cloud sync complete.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Auth] Cloud sync failed. Records will retry on next drain cycle.");
            }
        });
    }
}
