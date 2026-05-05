namespace DigitalTwin.Mobile.OCR.Models.ML;

public sealed record ClassificationResult(
    string Type,
    float Confidence,
    string Method);
