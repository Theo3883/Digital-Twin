namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents a sleep session returned to the doctor portal.
/// </summary>
public record SleepSessionDto
{
    /// <summary>
    /// Gets the session start time.
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// Gets the session end time.
    /// </summary>
    public DateTime EndTime { get; init; }

    /// <summary>
    /// Gets the session duration in minutes.
    /// </summary>
    public int DurationMinutes { get; init; }

    /// <summary>
    /// Gets the sleep quality score.
    /// </summary>
    public double QualityScore { get; init; }
}
