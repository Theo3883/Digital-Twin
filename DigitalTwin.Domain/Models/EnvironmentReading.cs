using DigitalTwin.Domain.Enums;

namespace DigitalTwin.Domain.Models;

public class EnvironmentReading
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double PM25 { get; set; }
    public double PM10 { get; set; }
    public double O3 { get; set; }
    public double NO2 { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public AirQualityLevel AirQuality { get; set; }

    /// <summary>
    /// OpenWeatherMap AQI index: 1=Good, 2=Fair, 3=Moderate, 4=Poor, 5=Very Poor.
    /// </summary>
    public int AqiIndex { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
