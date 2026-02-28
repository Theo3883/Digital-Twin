namespace DigitalTwin.Infrastructure.Entities;

public class SleepSessionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public decimal QualityScore { get; set; }
    public bool IsDirty { get; set; }
    public DateTime? SyncedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public PatientEntity Patient { get; set; } = null!;
}
