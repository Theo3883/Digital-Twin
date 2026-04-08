namespace DigitalTwin.Domain.Models;

/// <summary>
/// Hourly 24h PM2.5 vs heart rate series and correlation summary for environment analytics.
/// </summary>
public sealed class EnvironmentAnalyticsResult
{
    public string HeartRatePath { get; init; } = string.Empty;

    public string Pm25Path { get; init; } = string.Empty;

    public double? CorrelationR { get; init; }

    public string Footnote { get; init; } = string.Empty;

    public bool HasPm25Series { get; init; }

    public bool HasHeartRateSeries { get; init; }
}
