using DigitalTwin.Domain.Enums;

namespace DigitalTwin.Domain.Events;

public class RiskEvent
{
    public AirQualityLevel AirQualityLevel { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
