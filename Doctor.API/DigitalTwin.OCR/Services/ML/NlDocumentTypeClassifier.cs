using DigitalTwin.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services.ML;

/// <summary>
/// iOS-only: loads doc_type_classifier_v1.mlmodelc from the app bundle and
/// runs on-device text classification via Apple's NaturalLanguage.NLModel.
///
/// The model is produced by scripts/train.sh and compiled with xcrun coremlcompiler.
/// Inference is fully offline — no network calls.
/// </summary>
public sealed class NlDocumentTypeClassifier : IDocumentTypeClassifier
{
    private const string ModelBundleName = "doc_type_classifier_v1.mlmodelc";
    private readonly ILogger<NlDocumentTypeClassifier> _logger;

    public NlDocumentTypeClassifier(ILogger<NlDocumentTypeClassifier> logger)
        => _logger = logger;

#if IOS || MACCATALYST
    private NaturalLanguage.NLModel? _model;
    private bool _loadAttempted;

    private NaturalLanguage.NLModel? LoadModel()
    {
        if (_loadAttempted) return _model;
        _loadAttempted = true;

        try
        {
            var bundle = Foundation.NSBundle.MainBundle;
            var modelPath = bundle.PathForResource(
                System.IO.Path.GetFileNameWithoutExtension(ModelBundleName),
                System.IO.Path.GetExtension(ModelBundleName).TrimStart('.'),
                "Models");

            if (modelPath is null)
            {
                _logger.LogWarning("[NL Classifier] Model not found in bundle: {Name}", ModelBundleName);
                return null;
            }

            var modelUrl = Foundation.NSUrl.FromFilename(modelPath);
            var coremlModel = CoreML.MLModel.Create(modelUrl, out var mlError);
            if (mlError is not null || coremlModel is null)
            {
                _logger.LogWarning("[NL Classifier] Failed to load CoreML model: {Err}", mlError);
                return null;
            }

            _model = NaturalLanguage.NLModel.Create(coremlModel, out var nlError);
            if (nlError is not null || _model is null)
                _logger.LogWarning("[NL Classifier] Failed to create NLModel: {Err}", nlError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NL Classifier] Unexpected error loading model.");
        }

        return _model;
    }

    public Task<ClassificationResult> ClassifyAsync(
        string ocrText, string? imagePath, CancellationToken ct = default)
    {
        var model = LoadModel();
        if (model is null)
            return Task.FromResult(Unknown("nl_model_unavailable"));

        try
        {
            // NLModel bindings expose GetPredictedLabel(string) in .NET iOS.
            // Confidence is not directly available; we treat this as a strong signal
            // and let the fusion thresholding happen at the orchestrator layer.
            var label = model.GetPredictedLabel(ocrText);
            if (string.IsNullOrWhiteSpace(label))
                return Task.FromResult(Unknown("nl_no_label"));

            var docType = ParseLabel(label);
            var confidence = docType == MedicalDocumentType.Unknown ? 0f : 0.90f;

            _logger.LogDebug("[NL Classifier] Label={Label} Conf={Conf:F3}", label, confidence);
            return Task.FromResult(new ClassificationResult(docType, confidence, "nl_model"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NL Classifier] Inference error.");
            return Task.FromResult(Unknown("nl_inference_error"));
        }
    }
#else
    public Task<ClassificationResult> ClassifyAsync(
        string ocrText, string? imagePath, CancellationToken ct = default)
        => Task.FromResult(Unknown("platform_not_supported"));
#endif

    private static ClassificationResult Unknown(string method)
        => new(MedicalDocumentType.Unknown, 0f, method);

    private static MedicalDocumentType ParseLabel(string label) => label switch
    {
        "Prescription"       => MedicalDocumentType.Prescription,
        "Referral"           => MedicalDocumentType.Referral,
        "LabResult"          => MedicalDocumentType.LabResult,
        "Discharge"          => MedicalDocumentType.Discharge,
        "MedicalCertificate" => MedicalDocumentType.MedicalCertificate,
        "ImagingReport"      => MedicalDocumentType.ImagingReport,
        "EcgReport"          => MedicalDocumentType.EcgReport,
        "OperativeReport"    => MedicalDocumentType.OperativeReport,
        "ConsultationNote"   => MedicalDocumentType.ConsultationNote,
        "GenericClinicForm"  => MedicalDocumentType.GenericClinicForm,
        _                    => MedicalDocumentType.Unknown
    };
}
