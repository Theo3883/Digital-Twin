namespace DigitalTwin.Mobile.OCR.Models.Structured;

public sealed record ExtractedMedication(
    ExtractedField<string> Name,
    ExtractedField<string>? Dose,
    ExtractedField<string>? Frequency,
    ExtractedField<string>? Route,
    ExtractedField<string>? Duration);
