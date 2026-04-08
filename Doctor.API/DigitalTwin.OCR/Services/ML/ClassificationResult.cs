using DigitalTwin.Domain.Enums;

namespace DigitalTwin.OCR.Services.ML;

/// <summary>Result returned by any IDocumentTypeClassifier implementation.</summary>
public sealed record ClassificationResult(
    MedicalDocumentType Type,
    float Confidence,
    /// <summary>Which layer produced this result: "keyword", "nl_model", "feature_print", "fusion".</summary>
    string Method);
