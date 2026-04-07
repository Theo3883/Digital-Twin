namespace DigitalTwin.OCR.Models.Graph;

/// <summary>A contiguous text block (paragraph / observation) from Apple Vision.</summary>
public sealed record OcrGraphBlock(
    int BlockIndex,
    IReadOnlyList<OcrGraphLine> Lines,
    OcrBoundingBox BoundingBox);
