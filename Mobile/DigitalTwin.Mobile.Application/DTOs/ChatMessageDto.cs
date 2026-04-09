namespace DigitalTwin.Mobile.Application.DTOs;

public record ChatMessageDto
{
    public Guid Id { get; init; }
    public string Content { get; init; } = string.Empty;
    public bool IsUser { get; init; }
    public DateTime Timestamp { get; init; }
}

public record CoachingAdviceDto
{
    public string Advice { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
