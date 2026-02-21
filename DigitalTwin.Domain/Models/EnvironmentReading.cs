using DigitalTwin.Domain.Enums;

namespace DigitalTwin.Domain.Models;

public class EnvironmentReading
{
    public double PM25 { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public AirQualityLevel AirQuality { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
