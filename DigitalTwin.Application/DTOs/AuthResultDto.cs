namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Immutable authentication result returned after a successful sign-in or registration.
/// </summary>
public record AuthResultDto
{
    /// <summary>
    /// Gets the authenticated user identifier.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Gets the authenticated user's email address.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display name built for the authenticated user.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the profile photo URL for the authenticated user.
    /// </summary>
    public string? PhotoUrl { get; init; }

    /// <summary>
    /// Gets a value indicating whether the user already has a patient profile.
    /// </summary>
    public bool HasPatientProfile { get; init; }
}

