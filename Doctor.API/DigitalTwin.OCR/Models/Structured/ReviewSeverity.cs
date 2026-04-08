namespace DigitalTwin.OCR.Models.Structured;

public enum ReviewSeverity
{
    /// <summary>Nice-to-have fields — auto-append proceeds normally.</summary>
    Info,
    /// <summary>Important fields — auto-append proceeds but marked for doctor review.</summary>
    Warning,
    /// <summary>Critical fields (patient identity, diagnosis) — auto-append is blocked.</summary>
    Critical
}
