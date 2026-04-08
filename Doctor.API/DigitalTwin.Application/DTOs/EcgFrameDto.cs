namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents a single ECG stream frame with waveform and derived measurements.
/// </summary>
public class EcgFrameDto
{
    /// <summary>
    /// Gets or sets the ECG waveform samples for the frame.
    /// </summary>
    public double[] Samples { get; set; } = [];

    /// <summary>
    /// Gets or sets the blood oxygen saturation reading associated with the frame.
    /// </summary>
    public double SpO2 { get; set; }

    /// <summary>
    /// Gets or sets the heart rate associated with the frame.
    /// </summary>
    public int HeartRate { get; set; }

    /// <summary>
    /// Gets or sets when the frame was captured.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
