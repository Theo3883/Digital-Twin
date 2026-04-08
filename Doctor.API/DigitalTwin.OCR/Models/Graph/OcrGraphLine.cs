namespace DigitalTwin.OCR.Models.Graph;

/// <summary>A text line within a block, carrying its tokens and merged bounding box.</summary>
public sealed record OcrGraphLine(
    int LineIndex,
    IReadOnlyList<OcrToken> Tokens,
    OcrBoundingBox BoundingBox,
    string Text,
    float AverageConfidence);
