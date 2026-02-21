namespace IOSHealthApp.Infrastructure.Entities;

public class VitalSignEntity
{
    public long Id { get; set; }
    public long PatientId { get; set; }
    public int Type { get; set; }
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Source { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public PatientEntity Patient { get; set; } = null!;
}
