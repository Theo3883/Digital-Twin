using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

public interface IAuthApplicationService
{
    /// <summary>
    /// Step 1: Authenticate with Google and check if user exists in DB.
    /// Returns the Google profile data and whether the user is new.
    /// Does NOT create any records for new users.
    /// </summary>
    Task<GoogleAuthCheckResult> AuthenticateWithGoogleAsync();

    /// <summary>
    /// Step 2 (new users only): Creates User + UserOAuth with the
    /// Google data from step 1 combined with the profile data from the form.
    /// </summary>
    Task<AuthResultDto> CompleteRegistrationAsync(ProfileCompletionDto profile);

    /// <summary>
    /// Creates or updates a Patient medical profile linked to the current user.
    /// Called from the Profile page after registration.
    /// </summary>
    Task<AuthResultDto> CreatePatientProfileAsync(PatientProfileDto profile);

    /// <summary>
    /// Returns the current user's Patient profile for display, or null if none exists.
    /// </summary>
    Task<PatientDisplayDto?> GetPatientProfileAsync();

    /// <summary>
    /// Full sign-in for returning users (called internally or when user already exists).
    /// </summary>
    Task<AuthResultDto> SignInExistingUserAsync();

    Task SignOutAsync();
    Task<AuthResultDto?> GetCurrentUserAsync();
    Task<long?> GetCurrentPatientIdAsync();
}

