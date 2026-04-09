namespace DigitalTwin.Mobile.Application.DTOs;

public record OcrDocumentDto
{
    public Guid Id { get; init; }
    public string OpaqueInternalName { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public int PageCount { get; init; }
    public string SanitizedOcrPreview { get; init; } = string.Empty;
    public DateTime ScannedAt { get; init; }
    public bool IsDirty { get; init; }
}

public record MedicalHistoryEntryDto
{
    public Guid Id { get; init; }
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
}
