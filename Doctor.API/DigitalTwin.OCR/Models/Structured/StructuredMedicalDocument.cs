using DigitalTwin.Domain.Enums;

namespace DigitalTwin.OCR.Models.Structured;

/// <summary>
/// The fully structured output of the ML extraction pipeline for a single scanned document.
/// Produced by StructuredDocumentBuilder after classification + field extraction.
/// </summary>
public sealed record StructuredMedicalDocument
{
    public required Guid DocumentId { get; init; }
    public required MedicalDocumentType DocumentType { get; init; }
    public required float ClassificationConfidence { get; init; }
    public required string ClassificationMethod { get; init; }
    public required ExtractionMethod PrimaryExtractionMethod { get; init; }

    // ── Patient identity ─────────────────────────────────────────────────────
    public ExtractedField<string>? PatientName { get; init; }
    public ExtractedField<string>? DateOfBirth { get; init; }
    public ExtractedField<string>? PatientId { get; init; }

    // ── Provider ─────────────────────────────────────────────────────────────
    public ExtractedField<string>? DoctorName { get; init; }
    public ExtractedField<string>? FacilityName { get; init; }
    public ExtractedField<string>? Specialty { get; init; }
    public ExtractedField<string>? DestinationClinic { get; init; }

    // ── Clinical ─────────────────────────────────────────────────────────────
    public ExtractedField<string>? Diagnosis { get; init; }
    public ExtractedField<string>? Recommendation { get; init; }
    public ExtractedField<string>? ClinicalNotes { get; init; }
    public ExtractedField<string>? ReportDate { get; init; }

    // ── Medications ──────────────────────────────────────────────────────────
    public IReadOnlyList<ExtractedMedication> Medications { get; init; } = [];

    // ── Lab results ──────────────────────────────────────────────────────────
    public IReadOnlyList<ExtractedLabResult> LabResults { get; init; } = [];

    // ── Metadata ─────────────────────────────────────────────────────────────
    public required DateTime ExtractedAt { get; init; }
    public required DocumentExtractionMetrics Metrics { get; init; }
    public IReadOnlyList<ReviewFlag> ReviewFlags { get; init; } = [];

    /// <summary>True when any critical review flag is present.</summary>
    public bool RequiresReview => ReviewFlags.Any(f => f.Severity == ReviewSeverity.Critical);
}
