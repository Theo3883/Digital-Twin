namespace DigitalTwin.OCR.Models.Structured;

/// <summary>A field that requires human review because ML confidence was below threshold.</summary>
public sealed record ReviewFlag(
    string FieldName,
    string Reason,
    ReviewSeverity Severity);
