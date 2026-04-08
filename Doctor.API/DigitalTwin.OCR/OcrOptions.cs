using DigitalTwin.OCR.Models.Enums;

namespace DigitalTwin.OCR;

public sealed class OcrOptions
{
    public SecurityMode SecurityMode { get; set; } = SecurityMode.Strict;

    /// <summary>Use Vision Accurate mode (higher quality, slower) vs Fast mode.</summary>
    public bool UseAccurateOcr { get; set; } = true;

    /// <summary>Maximum preview characters shown in the sanitized result panel.</summary>
    public int MaxSanitizedPreviewLength { get; set; } = 2000;

    // ── ML feature flags (all off by default — purely additive) ──────────────

    /// <summary>
    /// Enable the ML document-type classifier (NL Text Classifier + Vision Feature Print).
    /// When false the existing keyword classifier runs as the sole signal.
    /// </summary>
    public bool UseMlClassification { get; set; } = false;

    /// <summary>
    /// Enable the BERT-based field extractor for structured extraction.
    /// When false heuristic/regex extraction is used.
    /// </summary>
    public bool UseMlExtraction { get; set; } = false;

    /// <summary>
    /// Minimum ML confidence required before accepting an ML result.
    /// Below this threshold the fusion falls back to the next layer.
    /// </summary>
    public float MlConfidenceThreshold { get; set; } = 0.65f;

    // ── Identity extraction flags ────────────────────────────────────────────

    /// <summary>
    /// Enable the improved identity extractor that uses PDF-text extraction (when available),
    /// OCR graph tokens, and digit/anchor normalization for more robust Name/CNP parsing.
    /// </summary>
    public bool UseIdentityV2 { get; set; } = false;
}
