namespace DigitalTwin.Domain.Models;

public class SleepSession
{
    public Guid PatientId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public double QualityScore { get; set; }
}
