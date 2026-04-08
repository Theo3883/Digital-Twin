using DigitalTwin.Application.Enums;

namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents a vital-sign sample exposed by the application layer.
/// </summary>
public class VitalSignDto
{
    /// <summary>
    /// Gets or sets the vital-sign type.
    /// </summary>
    public VitalSignType Type { get; set; }

    /// <summary>
    /// Gets or sets the measured value.
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Gets or sets the measurement unit.
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the sample was recorded.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the UI trend indicator where 1 is rising, -1 is falling, and 0 is stable.
    /// </summary>
    public int Trend { get; set; }
}
