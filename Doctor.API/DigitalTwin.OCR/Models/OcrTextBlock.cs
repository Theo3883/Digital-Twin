namespace DigitalTwin.OCR.Models;

public record OcrTextBlock(
    string Text,
    float Confidence,
    IReadOnlyList<OcrLine> Lines);
