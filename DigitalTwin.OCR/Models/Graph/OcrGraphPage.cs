namespace DigitalTwin.OCR.Models.Graph;

/// <summary>All graph elements for a single document page.</summary>
public sealed record OcrGraphPage(
    int PageIndex,
    IReadOnlyList<OcrGraphBlock> Blocks,
    IReadOnlyList<OcrGraphLine> Lines,
    IReadOnlyList<OcrToken> Tokens,
    float PageWidth,
    float PageHeight);
