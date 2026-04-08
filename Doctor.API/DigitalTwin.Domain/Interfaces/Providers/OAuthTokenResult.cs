namespace DigitalTwin.Domain.Interfaces.Providers;

/// <summary>
/// Immutable result returned by <see cref="IOAuthTokenProvider"/> after a successful
/// OAuth flow. All properties are set at construction time.
/// </summary>
public record OAuthTokenResult
{
    public string ProviderUserId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public DateTime? ExpiresAt { get; init; }
}
