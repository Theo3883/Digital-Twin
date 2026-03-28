namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Precomputed 24h heart rate vs PM2.5 series for the Environment chart (hourly buckets).
/// </summary>
public sealed class EnvironmentAnalyticsDto
{
    /// <summary>SVG path <c>d</c> for heart rate (viewBox 0 0 100 50).</summary>
    public string HeartRatePath { get; set; } = string.Empty;

    /// <summary>SVG path <c>d</c> for PM2.5 (same viewBox).</summary>
    public string Pm25Path { get; set; } = string.Empty;

    public double? CorrelationR { get; set; }

    /// <summary>Explains correlation, data gaps, or thresholds.</summary>
    public string Footnote { get; set; } = string.Empty;

    public bool HasPm25Series { get; set; }

    public bool HasHeartRateSeries { get; set; }
}
