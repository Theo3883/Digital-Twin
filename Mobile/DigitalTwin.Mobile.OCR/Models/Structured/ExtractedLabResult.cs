namespace DigitalTwin.Mobile.OCR.Models.Structured;

public sealed record ExtractedLabResult(
    ExtractedField<string> AnalysisName,
    ExtractedField<string> Value,
    ExtractedField<string>? Unit,
    ExtractedField<string>? ReferenceRange,
    ExtractedField<string>? SampleDate,
    bool IsOutOfRange);
