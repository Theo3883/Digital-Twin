using DigitalTwin.Domain.Enums;
using DigitalTwin.OCR.Services;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services.ML;

/// <summary>
/// Fusion classifier: combines keyword layer, NL Text Classifier, and Vision Feature Print
/// in a priority waterfall. All cross-platform C# — no native API calls here.
///
/// Decision logic:
///   1. Always run keyword AND NL classifier.
///   2. If keyword != Unknown and NL is confident:
///        - If they agree → accept (method: agreement)
///        - If they disagree → return Unknown (force review)
///   3. If keyword == Unknown:
///        - If NL is confident → accept
///        - Else try FeaturePrint
///   4. Otherwise Unknown.
/// </summary>
public sealed class ClassificationOrchestrator : IDocumentTypeClassifier
{
    private readonly NlDocumentTypeClassifier _nlClassifier;
    private readonly FeaturePrintDocumentClassifier _featurePrint;
    private readonly float _confidenceThreshold;
    private readonly ILogger<ClassificationOrchestrator> _logger;

    public ClassificationOrchestrator(
        NlDocumentTypeClassifier nlClassifier,
        FeaturePrintDocumentClassifier featurePrint,
        OcrOptions options,
        ILogger<ClassificationOrchestrator> logger)
    {
        _nlClassifier = nlClassifier;
        _featurePrint = featurePrint;
        _confidenceThreshold = options.MlConfidenceThreshold;
        _logger = logger;
    }

    public async Task<ClassificationResult> ClassifyAsync(
        string ocrText, string? imagePath, CancellationToken ct = default)
    {
        // Layer 1 — Keyword (always on, O(1))
        var keywordType = DocumentTypeClassifierService.Classify(ocrText);

        // Layer 2 — NL Text Classifier (always run for validation)
        var nlResult = await _nlClassifier.ClassifyAsync(ocrText, imagePath, ct);

        var nlIsConfident = nlResult.Type != MedicalDocumentType.Unknown
                            && nlResult.Confidence >= _confidenceThreshold;

        if (keywordType != MedicalDocumentType.Unknown)
        {
            if (!nlIsConfident)
            {
                // Keyword has a signal; NL unavailable/low-confidence → accept keyword but record that validation was weak.
                _logger.LogDebug(
                    "[Orchestrator] Keyword accepted (NL not confident). Keyword={Keyword} NL={Nl} ({NlConf:F3})",
                    keywordType, nlResult.Type, nlResult.Confidence);
                return new ClassificationResult(keywordType, 1.0f, "keyword");
            }

            if (nlResult.Type == keywordType)
            {
                _logger.LogDebug(
                    "[Orchestrator] Keyword and NL agree: {Type} (NL Conf={Conf:F3})",
                    keywordType, nlResult.Confidence);
                // Keep keyword as the method-of-record (fast path) but note agreement.
                return new ClassificationResult(keywordType, 1.0f, "keyword+nl_agree");
            }

            // Disagreement between keyword and confident NL → force review (return Unknown)
            _logger.LogWarning(
                "[Orchestrator] DISAGREE keyword vs NL. Keyword={Keyword} NL={Nl} ({NlConf:F3}). Returning Unknown.",
                keywordType, nlResult.Type, nlResult.Confidence);
            return new ClassificationResult(MedicalDocumentType.Unknown, 0f, "keyword_vs_nl_disagree");
        }

        // Keyword has no signal. If NL is confident, accept it.
        if (nlIsConfident)
        {
            _logger.LogDebug("[Orchestrator] NL accepted: {Type} ({Conf:F3})", nlResult.Type, nlResult.Confidence);
            return nlResult with { Method = "nl_model" };
        }

        // Layer 3 — Vision Feature Print (visual nearest-neighbour)
        if (imagePath is not null)
        {
            var fpResult = await _featurePrint.ClassifyAsync(ocrText, imagePath, ct);
            if (fpResult.Type != MedicalDocumentType.Unknown
                && fpResult.Confidence >= _confidenceThreshold)
            {
                _logger.LogDebug("[Orchestrator] FeaturePrint layer accepted: {Type} ({Conf:F3})",
                    fpResult.Type, fpResult.Confidence);
                return fpResult with { Method = "feature_print" };
            }
        }

        _logger.LogInformation(
            "[Orchestrator] All layers inconclusive. Keyword={Keyword} NL={Nl} ({NlConf:F3}). Returning Unknown.",
            keywordType, nlResult.Type, nlResult.Confidence);
        return new ClassificationResult(MedicalDocumentType.Unknown, 0f, "all_layers_inconclusive");
    }
}
