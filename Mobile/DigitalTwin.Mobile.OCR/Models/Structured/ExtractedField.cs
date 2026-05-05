namespace DigitalTwin.Mobile.OCR.Models.Structured;

public sealed record ExtractedField<T>(
    T Value,
    float Confidence,
    ExtractionMethod Method)
{
    public bool NeedsReview => Confidence < 0.70f;
}
