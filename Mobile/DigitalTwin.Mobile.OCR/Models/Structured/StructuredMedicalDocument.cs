namespace DigitalTwin.Mobile.OCR.Models.Structured;

/// <summary>
/// The fully structured output of the extraction pipeline.
/// Produced by StructuredDocumentBuilder after classification + field extraction.
/// </summary>
public sealed class StructuredMedicalDocument
{
    public Guid DocumentId { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public float ClassificationConfidence { get; init; }
    public string ClassificationMethod { get; init; } = string.Empty;
    public ExtractionMethod PrimaryExtractionMethod { get; init; }

    public ExtractedField<string>? PatientName { get; init; }
    public ExtractedField<string>? PatientId { get; init; }
    public ExtractedField<string>? ReportDate { get; init; }
    public ExtractedField<string>? DoctorName { get; init; }
    public ExtractedField<string>? Diagnosis { get; init; }

    public IReadOnlyList<ExtractedMedication> Medications { get; init; } = [];
    public IReadOnlyList<ExtractedLabResult> LabResults { get; init; } = [];

    public DateTime ExtractedAt { get; init; } = DateTime.UtcNow;
    public IReadOnlyList<ReviewFlag> ReviewFlags { get; init; } = [];
    public DocumentExtractionMetrics? Metrics { get; init; }

    public bool RequiresReview => ReviewFlags.Any(f => f.Severity == ReviewSeverity.Critical);
}
