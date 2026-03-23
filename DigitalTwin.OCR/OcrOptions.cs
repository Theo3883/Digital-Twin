using DigitalTwin.OCR.Models.Enums;

namespace DigitalTwin.OCR;

public sealed class OcrOptions
{
    public SecurityMode SecurityMode { get; set; } = SecurityMode.Strict;

    /// <summary>Use Vision Accurate mode (higher quality, slower) vs Fast mode.</summary>
    public bool UseAccurateOcr { get; set; } = true;

    /// <summary>Maximum preview characters shown in the sanitized result panel.</summary>
    public int MaxSanitizedPreviewLength { get; set; } = 2000;
}
