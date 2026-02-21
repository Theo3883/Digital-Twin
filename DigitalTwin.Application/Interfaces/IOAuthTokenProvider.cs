namespace DigitalTwin.Application.Interfaces;

public class OAuthTokenResult
{
    public string ProviderUserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public interface IOAuthTokenProvider
{
    Task<OAuthTokenResult> GetTokensAsync();
}
