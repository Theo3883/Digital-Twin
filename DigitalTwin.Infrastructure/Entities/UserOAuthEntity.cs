namespace DigitalTwin.Infrastructure.Entities;

public class UserOAuthEntity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public int Provider { get; set; }
    public string ProviderUserId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDirty { get; set; } = true;
    public DateTime? SyncedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public UserEntity User { get; set; } = null!;
}
