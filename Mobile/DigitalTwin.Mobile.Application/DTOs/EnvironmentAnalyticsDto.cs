namespace DigitalTwin.Mobile.Application.DTOs;

public sealed record EnvironmentAnalyticsDto
{
    public double? CorrelationR { get; init; }
    public string Footnote { get; init; } = string.Empty;
    public HourlyDataPoint[] HeartRateSeries { get; init; } = [];
    public HourlyDataPoint[] Pm25Series { get; init; } = [];
}

public sealed record HourlyDataPoint
{
    public int Hour { get; init; }
    public double Value { get; init; }
}
