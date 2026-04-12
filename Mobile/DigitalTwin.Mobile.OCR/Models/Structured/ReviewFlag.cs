namespace DigitalTwin.Mobile.OCR.Models.Structured;

public sealed record ReviewFlag(
    string FieldName,
    string Reason,
    ReviewSeverity Severity);
