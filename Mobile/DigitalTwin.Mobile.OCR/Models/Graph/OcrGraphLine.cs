namespace DigitalTwin.Mobile.OCR.Models.Graph;

public sealed record OcrGraphLine(
    int LineIndex,
    IReadOnlyList<OcrToken> Tokens,
    OcrBoundingBox BoundingBox,
    string Text,
    float AverageConfidence);
