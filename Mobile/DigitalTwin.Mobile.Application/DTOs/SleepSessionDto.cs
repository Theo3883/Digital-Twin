namespace DigitalTwin.Mobile.Application.DTOs;

public record SleepSessionDto
{
    public Guid Id { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public int DurationMinutes { get; init; }
    public double QualityScore { get; init; }
    public bool IsSynced { get; init; }
}

public record SleepSessionInput
{
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public int DurationMinutes { get; init; }
    public double QualityScore { get; init; }
}
