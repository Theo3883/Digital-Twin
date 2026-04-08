using DigitalTwin.Mobile.Domain.Enums;

namespace DigitalTwin.Mobile.Application.DTOs;

public record VitalSignDto
{
    public Guid Id { get; init; }
    public VitalSignType Type { get; init; }
    public double Value { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public bool IsSynced { get; init; }
}

public record VitalSignInput
{
    public VitalSignType Type { get; init; }
    public double Value { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string Source { get; init; } = "Manual";
    public DateTime? Timestamp { get; init; }
}