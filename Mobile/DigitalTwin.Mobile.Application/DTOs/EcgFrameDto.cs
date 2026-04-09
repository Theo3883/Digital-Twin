using DigitalTwin.Mobile.Domain.Enums;

namespace DigitalTwin.Mobile.Application.DTOs;

public record EcgFrameDto
{
    public double[] Samples { get; init; } = [];
    public double SpO2 { get; init; }
    public int HeartRate { get; init; }
    public DateTime Timestamp { get; init; }
    public string TriageResult { get; init; } = "Pass";
}

public record CriticalAlertDto
{
    public string RuleName { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
