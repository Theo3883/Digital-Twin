namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Structured medical-history entry (doctor portal).
/// </summary>
public record MedicalHistoryEntryDto
{
    public Guid Id { get; init; }
    public Guid PatientId { get; init; }
    public Guid SourceDocumentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string MedicationName { get; init; } = string.Empty;
    public string Dosage { get; init; } = string.Empty;
    public string Frequency { get; init; } = string.Empty;
    public string Duration { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public decimal Confidence { get; init; }
    public DateTime EventDate { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

