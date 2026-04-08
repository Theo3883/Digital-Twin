using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Represents the result of the initial Google authentication check.
/// </summary>
public record GoogleAuthCheckResult
{
    /// <summary>
    /// Gets a value indicating whether the authenticated Google user already has an account.
    /// </summary>
    public bool IsExistingUser { get; init; }

    /// <summary>
    /// Gets the completed authentication result for an existing user.
    /// </summary>
    public AuthResultDto? AuthResult { get; init; }

    /// <summary>
    /// Gets the email address returned by Google.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Gets the first name returned by Google.
    /// </summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the last name returned by Google.
    /// </summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Google profile photo URL, if available.
    /// </summary>
    public string? PhotoUrl { get; init; }
}
