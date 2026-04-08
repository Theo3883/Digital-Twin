using DigitalTwin.Application.Enums;

namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents a risk event raised from assessed environment conditions.
/// </summary>
public class RiskEventDto
{
    /// <summary>
    /// Gets or sets the assessed air-quality level that triggered the event.
    /// </summary>
    public AirQualityLevel AirQualityLevel { get; set; }

    /// <summary>
    /// Gets or sets the event message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the event was generated.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
