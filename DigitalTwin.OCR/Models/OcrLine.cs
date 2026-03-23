namespace DigitalTwin.OCR.Models;

public record OcrLine(
    string Text,
    float Confidence,
    float X,
    float Y,
    float Width,
    float Height);
