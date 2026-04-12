using DigitalTwin.Mobile.OCR.Models.Structured;

namespace DigitalTwin.Mobile.OCR.Models.ML;

public sealed record BertExtractionResult(
    bool IsAvailable,
    ExtractedField<string>? PatientName,
    ExtractedField<string>? DoctorName,
    ExtractedField<string>? Diagnosis,
    ExtractedField<string>? PatientId,
    IReadOnlyList<ExtractedField<string>> Medications)
{
    public static BertExtractionResult Unavailable =>
        new(false, null, null, null, null, []);
}
