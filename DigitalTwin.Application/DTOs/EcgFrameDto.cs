namespace DigitalTwin.Application.DTOs;

public class EcgFrameDto
{
    public double[] Samples { get; set; } = [];
    public double SpO2 { get; set; }
    public int HeartRate { get; set; }
    public DateTime Timestamp { get; set; }
}
