using DigitalTwin.OCR.Models.Enums;

namespace DigitalTwin.OCR.Models;

public record OcrExtractionResult(
    IReadOnlyList<OcrPage> Pages,
    OcrExecutionStatus OverallStatus,
    string? DetectedLanguage,
    bool IsRomanianSupported,
    string? FailureReason = null)
{
    public string RawText => string.Join("\n", Pages.SelectMany(p => p.Blocks).Select(b => b.Text));
}
