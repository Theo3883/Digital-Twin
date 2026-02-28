namespace DigitalTwin.Domain.Models;

/// <summary>
/// Immutable result of the initial Google authentication check.
/// Tells the caller whether to show the profile form (new user) or proceed directly (returning user).
/// </summary>
public record GoogleAuthCheckResult
{
    public bool IsExistingUser { get; init; }
    public User? User { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
}
