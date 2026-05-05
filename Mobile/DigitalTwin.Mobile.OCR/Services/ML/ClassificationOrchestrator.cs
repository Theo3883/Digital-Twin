using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.OCR.Models.ML;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.OCR.Services.ML;

/// <summary>
/// Fusion classifier: combines keyword layer with optional ML result strings
/// in a priority waterfall. Pure C# — no native API calls.
///
/// Decision logic:
///   1. Always run keyword classifier.
///   2. If an ML result string is supplied (from Swift-side NL/FeaturePrint):
///      - If keyword and ML agree → accept (method: keyword+ml_agree)
///      - If they disagree and ML is confident → return Unknown (force review)
///   3. If keyword == Unknown and ML is confident → accept ML
///   4. Otherwise Unknown.
/// </summary>
public sealed class ClassificationOrchestrator
{
    private readonly IDocumentTypeClassifier _keywordClassifier;
    private readonly float _confidenceThreshold;
    private readonly ILogger<ClassificationOrchestrator> _logger;

    public ClassificationOrchestrator(
        IDocumentTypeClassifier keywordClassifier,
        float confidenceThreshold,
        ILogger<ClassificationOrchestrator> logger)
    {
        _keywordClassifier = keywordClassifier;
        _confidenceThreshold = confidenceThreshold;
        _logger = logger;
    }

    public ClassificationResult Classify(
        string ocrText,
        string? mlType = null,
        float mlConfidence = 0f)
    {
        var keywordType = _keywordClassifier.Classify(ocrText);

        var mlIsConfident = mlType is not null
            && mlType != "Unknown"
            && mlConfidence >= _confidenceThreshold;

        if (keywordType != "Unknown")
        {
            if (!mlIsConfident)
            {
                _logger.LogDebug(
                    "[Orchestrator] Keyword accepted (ML not confident). Keyword={Keyword} ML={Ml} ({MlConf:F3})",
                    keywordType, mlType, mlConfidence);
                return new ClassificationResult(keywordType, 1.0f, "keyword");
            }

            if (mlType == keywordType)
            {
                _logger.LogDebug(
                    "[Orchestrator] Keyword and ML agree: {Type} (ML Conf={Conf:F3})",
                    keywordType, mlConfidence);
                return new ClassificationResult(keywordType, 1.0f, "keyword+ml_agree");
            }

            _logger.LogWarning(
                "[Orchestrator] DISAGREE keyword vs ML. Keyword={Keyword} ML={Ml} ({MlConf:F3}). Returning Unknown.",
                keywordType, mlType, mlConfidence);
            return new ClassificationResult("Unknown", 0f, "keyword_vs_ml_disagree");
        }

        if (mlIsConfident)
        {
            _logger.LogDebug("[Orchestrator] ML accepted: {Type} ({Conf:F3})", mlType, mlConfidence);
            return new ClassificationResult(mlType!, mlConfidence, "ml_model");
        }

        _logger.LogInformation(
            "[Orchestrator] All layers inconclusive. Keyword={Keyword} ML={Ml} ({MlConf:F3}). Returning Unknown.",
            keywordType, mlType, mlConfidence);
        return new ClassificationResult("Unknown", 0f, "all_layers_inconclusive");
    }
}
