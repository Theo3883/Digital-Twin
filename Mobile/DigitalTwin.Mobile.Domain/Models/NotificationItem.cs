namespace DigitalTwin.Mobile.Domain.Models;

public sealed record NotificationItem
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public int Type { get; init; }
    public int Severity { get; init; }
    public Guid RecipientUserId { get; init; }
    public Guid? ActorUserId { get; init; }
    public string? ActorName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ReadAt { get; init; }
    public bool IsUnread => ReadAt == null;
}
