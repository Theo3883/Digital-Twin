namespace DigitalTwin.OCR.Models.Graph;

/// <summary>
/// Normalised bounding box (0–1 coordinate space) of an OCR element.
/// Origin is bottom-left as returned by Apple Vision framework.
/// </summary>
public sealed record OcrBoundingBox(float X, float Y, float Width, float Height)
{
    public float CenterY => Y + Height / 2f;
    public float CenterX => X + Width / 2f;
    public float Bottom  => Y + Height;
    public float Right   => X + Width;

    /// <summary>True when the two boxes share the same text row (within toleranceY).</summary>
    public bool IsOnSameRow(OcrBoundingBox other, float toleranceY = 0.01f)
        => Math.Abs(CenterY - other.CenterY) < toleranceY;

    public static OcrBoundingBox Empty => new(0f, 0f, 0f, 0f);
}
