namespace DigitalTwin.Infrastructure.Entities;

public class VitalSignEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public int Type { get; set; }
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Source { get; set; }
    public bool IsDirty { get; set; }
    public DateTime? SyncedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public PatientEntity Patient { get; set; } = null!;
}
