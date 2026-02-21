namespace IOSHealthApp.Domain.Models;

public class SleepSession
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public double QualityScore { get; set; }
}
