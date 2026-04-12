namespace DigitalTwin.Mobile.OCR.Models.Structured;

public sealed record DocumentExtractionMetrics(
    int TotalTokens,
    float AverageFieldConfidence,
    TimeSpan OcrDuration,
    TimeSpan ClassificationDuration,
    TimeSpan ExtractionDuration);
