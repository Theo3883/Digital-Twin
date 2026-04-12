using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.OCR.Services;
using DigitalTwin.Mobile.OCR.Services.ML;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalTwin.Mobile.OCR;

public static class DependencyInjection
{
    /// <summary>
    /// Registers all Mobile OCR services into the DI container.
    /// <paramref name="vaultRoot"/> — absolute path to the vault directory (Swift passes this).
    /// <paramref name="vocabPath"/> — optional path to BERT vocab.txt file for WordPiece tokenizer.
    /// <paramref name="mlConfidenceThreshold"/> — minimum confidence for ML classification acceptance.
    /// </summary>
    public static IServiceCollection AddMobileOcr(
        this IServiceCollection services,
        string vaultRoot,
        string? vocabPath = null,
        float mlConfidenceThreshold = 0.70f)
    {
        // Core services implementing domain interfaces
        services.AddSingleton<ISensitiveDataSanitizer, SensitiveDataSanitizer>();
        services.AddSingleton<IDocumentTypeClassifier, DocumentTypeClassifier>();
        services.AddSingleton<INameMatchingService, NameMatchingService>();
        services.AddSingleton<IDocumentIdentityExtractor, DocumentIdentityExtractor>();
        services.AddSingleton<IDocumentIdentityValidator, DocumentIdentityValidator>();
        services.AddSingleton<IMedicalHistoryExtractor, MedicalHistoryExtractor>();
        services.AddSingleton<IHeuristicFieldExtractor, HeuristicFieldExtractor>();

        // Table extraction
        services.AddSingleton<GeometricTableExtractor>();

        // Encryption & vault
        services.AddSingleton<DocumentEncryptionService>();
        services.AddSingleton<HashingService>();
        services.AddSingleton(sp => new VaultService(
            vaultRoot,
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<VaultService>>()));

        // ML pipeline
        services.AddSingleton(WordPieceTokenizer.FromVocabPath(vocabPath));
        services.AddSingleton<BertFieldExtractor>();
        services.AddSingleton(sp => new ClassificationOrchestrator(
            sp.GetRequiredService<IDocumentTypeClassifier>(),
            mlConfidenceThreshold,
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ClassificationOrchestrator>>()));
        services.AddSingleton<MlPipelineAuditService>();

        // Document builder
        services.AddSingleton<StructuredDocumentBuilder>();

        return services;
    }
}
