using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Defines application-level authentication and patient onboarding operations.
/// </summary>
public interface IAuthApplicationService
{
    /// <summary>
    /// Starts Google authentication and reports whether the authenticated user already exists.
    /// </summary>
    Task<GoogleAuthCheckResult> AuthenticateWithGoogleAsync();

    /// <summary>
    /// Completes registration for a newly authenticated Google user using the supplied profile data.
    /// </summary>
    Task<AuthResultDto> CompleteRegistrationAsync(ProfileCompletionDto profile);

    /// <summary>
    /// Creates or updates the current user's patient profile.
    /// </summary>
    Task<AuthResultDto> CreatePatientProfileAsync(PatientProfileDto profile);

    /// <summary>
    /// Gets the current user's patient profile for display, if one exists.
    /// </summary>
    Task<PatientDisplayDto?> GetPatientProfileAsync();

    /// <summary>
    /// Signs in the current authenticated user when they already exist in the system.
    /// </summary>
    Task<AuthResultDto> SignInExistingUserAsync();

    /// <summary>
    /// Signs out the current user.
    /// </summary>
    Task SignOutAsync();

    /// <summary>
    /// Gets the cached or current authenticated user result, if available.
    /// </summary>
    Task<AuthResultDto?> GetCurrentUserAsync();

    /// <summary>
    /// Gets the current user's patient identifier, if a patient profile exists.
    /// </summary>
    Task<Guid?> GetCurrentPatientIdAsync();
}

