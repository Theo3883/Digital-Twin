namespace DigitalTwin.Infrastructure.Entities;

public class UserEntity
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public int Role { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDirty { get; set; }
    public DateTime? SyncedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public PatientEntity? Patient { get; set; }
    public ICollection<UserOAuthEntity> OAuthAccounts { get; set; } = [];
}
