using DigitalTwin.Application.Enums;

namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents an assessed environment reading returned by the application layer.
/// </summary>
public class EnvironmentReadingDto
{
    /// <summary>
    /// Gets or sets the PM2.5 concentration.
    /// </summary>
    public double PM25 { get; set; }

    /// <summary>
    /// Gets or sets the PM10 concentration.
    /// </summary>
    public double PM10 { get; set; }

    /// <summary>
    /// Gets or sets the ozone concentration.
    /// </summary>
    public double O3 { get; set; }

    /// <summary>
    /// Gets or sets the nitrogen dioxide concentration.
    /// </summary>
    public double NO2 { get; set; }

    /// <summary>
    /// Gets or sets the ambient temperature.
    /// </summary>
    public double Temperature { get; set; }

    /// <summary>
    /// Gets or sets the ambient humidity.
    /// </summary>
    public double Humidity { get; set; }

    /// <summary>
    /// Gets or sets the assessed air-quality level.
    /// </summary>
    public AirQualityLevel AirQuality { get; set; }

    /// <summary>
    /// Gets or sets the raw OpenWeatherMap AQI index.
    /// </summary>
    public int AqiIndex { get; set; }

    /// <summary>
    /// Gets or sets when the reading was produced.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
