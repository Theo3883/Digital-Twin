namespace DigitalTwin.Infrastructure.Entities;

public class NotificationEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipientUserId { get; set; }
    public int RecipientRole { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int Type { get; set; }
    public int Severity { get; set; }
    public Guid? PatientId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
