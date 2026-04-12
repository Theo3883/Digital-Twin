namespace DigitalTwin.Mobile.OCR.Models.Graph;

public sealed record OcrBoundingBox(float X, float Y, float Width, float Height)
{
    public float CenterY => Y + Height / 2f;
    public float CenterX => X + Width / 2f;
    public float Bottom => Y + Height;
    public float Right => X + Width;

    public bool IsOnSameRow(OcrBoundingBox other, float toleranceY = 0.01f)
        => Math.Abs(CenterY - other.CenterY) <= toleranceY;
}
