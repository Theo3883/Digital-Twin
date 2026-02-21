using DigitalTwin.Application.Enums;

namespace DigitalTwin.Application.DTOs;

public class RiskEventDto
{
    public AirQualityLevel AirQualityLevel { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
