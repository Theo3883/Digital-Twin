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
    public string SchemaVersion { get; init; } = "1.0";
    public string Headline { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public List<CoachingSectionDto> Sections { get; init; } = [];
    public List<CoachingActionDto> Actions { get; init; } = [];
    public string Motivation { get; init; } = string.Empty;
    public string SafetyNote { get; init; } = string.Empty;
    public string Advice { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

public record CoachingSectionDto
{
    public string Category { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public List<string> Items { get; init; } = [];
}

public record CoachingActionDto
{
    public string Category { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}
