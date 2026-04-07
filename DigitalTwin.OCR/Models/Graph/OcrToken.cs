namespace DigitalTwin.OCR.Models.Graph;

/// <summary>
/// A single word/token produced by OCR with its spatial position.
/// This is the atomic unit of the document graph fed to the ML pipeline.
/// </summary>
public sealed record OcrToken(
    int TokenIndex,
    string Text,
    float Confidence,
    OcrBoundingBox BoundingBox,
    int PageIndex,
    int BlockIndex,
    int LineIndex,
    /// <summary>True when the bounding box fell back to line-level (per-word box unavailable).</summary>
    bool IsBoundingBoxApproximate = false);
