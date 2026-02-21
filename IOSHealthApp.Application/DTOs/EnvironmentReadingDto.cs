using IOSHealthApp.Domain.Enums;

namespace IOSHealthApp.Application.DTOs;

public class EnvironmentReadingDto
{
    public double PM25 { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public AirQualityLevel AirQuality { get; set; }
    public DateTime Timestamp { get; set; }
}
