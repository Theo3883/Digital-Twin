namespace DigitalTwin.Mobile.Domain.Models;

/// <summary>
/// Identity fields extracted from a scanned document's raw OCR text.
/// </summary>
public sealed record DocumentIdentity(
    string? ExtractedName,
    string? ExtractedCnp,
    float NameConfidence,
    float CnpConfidence);

/// <summary>Result of a fuzzy name comparison.</summary>
public sealed record NameMatchResult(
    bool IsMatch,
    int Distance,
    string NormalizedExpected,
    string NormalizedActual);

/// <summary>Structured extraction result item.</summary>
public sealed record ExtractedHistoryItem(
    string Title,
    string MedicationName,
    string Dosage,
    string Frequency,
    string Duration,
    string Notes,
    string Summary,
    decimal Confidence);

/// <summary>Identity validation result.</summary>
public sealed record IdentityValidationResult(
    bool IsValid,
    bool NameMatched,
    bool CnpMatched,
    string? Reason);

/// <summary>Heuristic field extraction result.</summary>
public sealed record HeuristicExtractionResult(
    string? PatientName,
    string? PatientId,
    string? ReportDate,
    string? DoctorName,
    string? Diagnosis,
    IReadOnlyList<ExtractedMedicationField> Medications)
{
    public static HeuristicExtractionResult Empty { get; } = new(null, null, null, null, null, []);
}

/// <summary>Extracted medication from structured text.</summary>
public sealed record ExtractedMedicationField(
    string Name,
    string? Dosage,
    string? Frequency,
    string? Rest);

/// <summary>Full OCR text processing result.</summary>
public sealed record OcrTextProcessingResult(
    string DocumentType,
    DocumentIdentity? Identity,
    IdentityValidationResult? Validation,
    string SanitizedText,
    HeuristicExtractionResult? Extraction,
    IReadOnlyList<ExtractedHistoryItem> HistoryItems);
