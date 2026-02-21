using DigitalTwin.Domain.Enums;

namespace DigitalTwin.Domain.Models;

public class UserOAuth
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public OAuthProvider Provider { get; set; }
    public string ProviderUserId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
