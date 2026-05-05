using DigitalTwin.Mobile.Domain.Enums;

namespace DigitalTwin.Mobile.Application.DTOs;

public record EnvironmentReadingDto
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string LocationDisplayName { get; init; } = string.Empty;
    public double PM25 { get; init; }
    public double PM10 { get; init; }
    public double O3 { get; init; }
    public double NO2 { get; init; }
    public double Temperature { get; init; }
    public double Humidity { get; init; }
    public AirQualityLevel AirQuality { get; init; }
    public int AqiIndex { get; init; }
    public DateTime Timestamp { get; init; }
}
