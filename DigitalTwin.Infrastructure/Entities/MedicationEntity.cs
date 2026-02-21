namespace DigitalTwin.Infrastructure.Entities;

public class MedicationEntity
{
    public long Id { get; set; }
    public long PatientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string? Frequency { get; set; }
    public int Route { get; set; }
    public string? RxCui { get; set; }
    public string? Instructions { get; set; }
    public string? Reason { get; set; }
    public long? PrescribedByUserId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Status { get; set; }
    public string? DiscontinuedReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public PatientEntity Patient { get; set; } = null!;
}
