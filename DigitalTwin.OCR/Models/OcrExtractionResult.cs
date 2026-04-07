using DigitalTwin.OCR.Models.Enums;
using DigitalTwin.OCR.Models.Graph;

namespace DigitalTwin.OCR.Models;

public record OcrExtractionResult(
    IReadOnlyList<OcrPage> Pages,
    OcrExecutionStatus OverallStatus,
    string? DetectedLanguage,
    bool IsRomanianSupported,
    string? FailureReason = null)
{
    public string RawText => string.Join("\n", Pages.SelectMany(p => p.Blocks).Select(b => b.Text));

    /// <summary>
    /// Spatial token graph produced by the ML pipeline extension.
    /// Null when graph building is disabled (UseMlClassification = false)
    /// or when running on non-iOS targets.
    /// </summary>
    public OcrDocumentGraph? Graph { get; init; }
}
