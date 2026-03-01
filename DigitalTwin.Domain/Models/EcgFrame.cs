namespace DigitalTwin.Domain.Models;

public class EcgFrame
{
    public double[] Samples { get; set; } = [];
    public double SpO2 { get; set; }
    public int HeartRate { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
