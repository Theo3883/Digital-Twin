namespace DigitalTwin.Domain.Interfaces.Providers;

/// <summary>
/// Immutable result of an OAuth token exchange (e.g. Google). Used when creating users via <see cref="IUserService.CreateUserAsync"/>.
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
