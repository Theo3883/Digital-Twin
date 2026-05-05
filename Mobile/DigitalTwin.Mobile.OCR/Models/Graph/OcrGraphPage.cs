namespace DigitalTwin.Mobile.OCR.Models.Graph;

public sealed record OcrGraphPage(
    int PageIndex,
    IReadOnlyList<OcrGraphBlock> Blocks,
    IReadOnlyList<OcrGraphLine> Lines,
    IReadOnlyList<OcrToken> Tokens,
    float PageWidth,
    float PageHeight);
