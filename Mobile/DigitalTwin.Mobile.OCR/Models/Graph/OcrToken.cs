namespace DigitalTwin.Mobile.OCR.Models.Graph;

public sealed record OcrToken(
    int TokenIndex,
    string Text,
    float Confidence,
    OcrBoundingBox BoundingBox,
    int PageIndex,
    int BlockIndex,
    int LineIndex,
    bool IsBoundingBoxApproximate = false);
