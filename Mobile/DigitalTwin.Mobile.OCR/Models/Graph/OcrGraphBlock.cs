namespace DigitalTwin.Mobile.OCR.Models.Graph;

public sealed record OcrGraphBlock(
    int BlockIndex,
    IReadOnlyList<OcrGraphLine> Lines,
    OcrBoundingBox BoundingBox);
