using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Immutable result of the initial Google authentication check.
/// Tells the UI whether to show the profile form (new user) or proceed directly (returning user).
/// </summary>
public record GoogleAuthCheckResult
{
    /// <summary>True when the user already has an account in the DB.</summary>
    public bool IsExistingUser { get; init; }

    /// <summary>If existing user, their full auth result (already signed in).</summary>
    public AuthResultDto? AuthResult { get; init; }

    /// <summary>Google profile data shown on the registration form for new users.</summary>
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
}
