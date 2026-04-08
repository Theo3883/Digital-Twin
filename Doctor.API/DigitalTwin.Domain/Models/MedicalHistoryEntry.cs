namespace DigitalTwin.Domain.Models;

/// <summary>
/// Structured medical-history entry extracted from OCR.
/// Includes full clinical details for doctor visibility and a patient-friendly summary.
/// </summary>
public class MedicalHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid SourceDocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string MedicationName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public DateTime EventDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDirty { get; set; } = true;
    public DateTime? SyncedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

