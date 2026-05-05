namespace DigitalTwin.Mobile.Application.DTOs;

/// <summary>
/// DTO for Swift bridge transport - mirrors Domain.NotificationItem
/// </summary>
public sealed record NotificationItemDto
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

    /// <summary>Map from domain model</summary>
    public static NotificationItemDto FromDomain(DigitalTwin.Mobile.Domain.Models.NotificationItem notification) =>
        new()
        {
            Id = notification.Id,
            Title = notification.Title,
            Body = notification.Body,
            Type = notification.Type,
            Severity = notification.Severity,
            RecipientUserId = notification.RecipientUserId,
            ActorUserId = notification.ActorUserId,
            ActorName = notification.ActorName,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt
        };
}
