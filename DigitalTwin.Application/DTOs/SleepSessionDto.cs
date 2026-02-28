namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Sleep session data returned to doctor portal.
/// </summary>
public record SleepSessionDto
{
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public int DurationMinutes { get; init; }
    public double QualityScore { get; init; }
}
