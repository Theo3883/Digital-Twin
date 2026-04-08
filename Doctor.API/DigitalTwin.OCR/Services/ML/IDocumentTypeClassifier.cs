namespace DigitalTwin.OCR.Services.ML;

/// <summary>
/// Unified interface for all document-type classifiers.
/// Implementations: ClassificationOrchestrator (fusion), NlDocumentTypeClassifier, FeaturePrintDocumentClassifier.
/// </summary>
public interface IDocumentTypeClassifier
{
    /// <summary>
    /// Classifies the document using OCR text and/or the image at imagePath.
    /// imagePath may be null when only text-based classification is possible.
    /// </summary>
    Task<ClassificationResult> ClassifyAsync(
        string ocrText,
        string? imagePath,
        CancellationToken ct = default);
}
