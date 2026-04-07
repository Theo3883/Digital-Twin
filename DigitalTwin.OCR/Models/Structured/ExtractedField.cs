namespace DigitalTwin.OCR.Models.Structured;

/// <summary>A single extracted field with its value, confidence, and provenance.</summary>
public sealed record ExtractedField<T>(
    T Value,
    float Confidence,
    ExtractionMethod Method)
{
    /// <summary>True when confidence is below the review threshold (0.70).</summary>
    public bool NeedsReview => Confidence < 0.70f;
}
