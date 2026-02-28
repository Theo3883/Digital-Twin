using DigitalTwin.Application.Enums;

namespace DigitalTwin.Application.DTOs;

public class VitalSignDto
{
    public VitalSignType Type { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// +1 rising, -1 falling, 0 stable
    /// </summary>
    public int Trend { get; set; }
}
