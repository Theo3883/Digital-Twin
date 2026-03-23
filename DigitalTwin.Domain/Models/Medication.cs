using DigitalTwin.Domain.Enums;

namespace DigitalTwin.Domain.Models;

public class Medication
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string? Frequency { get; set; }
    public MedicationRoute Route { get; set; }
    public string? RxCui { get; set; }
    public string? Instructions { get; set; }
    public string? Reason { get; set; }
    public Guid? PrescribedByUserId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public MedicationStatus Status { get; set; }
    public string? DiscontinuedReason { get; set; }
    public AddedByRole AddedByRole { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
