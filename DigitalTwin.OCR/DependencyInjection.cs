using DigitalTwin.OCR.Policies;
using DigitalTwin.OCR.Services;
using DigitalTwin.OCR.Services.Extraction;
using DigitalTwin.OCR.Services.ML;
using DigitalTwin.OCR.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalTwin.OCR;

/// <summary>
/// Registers all DigitalTwin.OCR services into the DI container.
/// Call from MauiProgram.cs inside the <c>registerIntegrations</c> callback.
/// </summary>
public static class OcrServiceCollectionExtensions
{
    public static IServiceCollection AddDigitalTwinOcr(
        this IServiceCollection services,
        Action<OcrOptions>? configure = null)
    {
        var options = new OcrOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        // ── Core services ────────────────────────────────────────────────────
        services.AddSingleton<OcrSheetService>();
        services.AddSingleton<IOcrSheetService>(sp => sp.GetRequiredService<OcrSheetService>());

        // ── Extraction pipeline ──────────────────────────────────────────────
        services.AddSingleton<HeuristicFieldExtractor>();
        services.AddSingleton<GeometricTableExtractor>();
        // StructuredDocumentBuilder receives BertFieldExtractor as optional dependency
        services.AddSingleton<StructuredDocumentBuilder>(sp => new StructuredDocumentBuilder(
            sp.GetRequiredService<HeuristicFieldExtractor>(),
            sp.GetRequiredService<GeometricTableExtractor>(),
            sp.GetService<BertFieldExtractor>()));

        // ── ML classification pipeline (on-device, iOS-only inference) ───────
        services.AddSingleton<NlDocumentTypeClassifier>();
        services.AddSingleton<FeaturePrintDocumentClassifier>();
        services.AddSingleton<ClassificationOrchestrator>();
        services.AddSingleton<IDocumentTypeClassifier>(sp =>
            sp.GetRequiredService<ClassificationOrchestrator>());

        // ── BERT field extractor (Phase 3, gated by UseMlExtraction) ─────────
        services.AddSingleton<WordPieceTokenizer>(_ => WordPieceTokenizer.FromBundledVocab());
        services.AddSingleton<BertFieldExtractor>();

        // ── Non-PII audit metrics (local-only, in-memory) ────────────────────
        services.AddSingleton<MlPipelineAuditService>();

        services.AddSingleton<DocumentEncryptionService>();
        services.AddSingleton<HashingService>();
        services.AddSingleton<SensitiveDataSanitizer>();
        services.AddSingleton<MedicalHistoryExtractionService>();
        services.AddSingleton<DocumentTypeClassifierService>();
        services.AddSingleton<DocumentIdentityExtractorService>();
        services.AddSingleton<NameMatchingService>();
        services.AddSingleton<DocumentIdentityValidationPolicy>();

        // ── iOS-backed services (gracefully no-op on non-iOS) ────────────────
        services.AddSingleton<KeychainKeyStore>();
        services.AddSingleton<FileProtectionService>();
        services.AddSingleton<SecurityService>();
        services.AddSingleton<LocalAuthenticationService>();
        services.AddSingleton<DocumentNormalizationService>();
        services.AddSingleton<LocalOcrService>();
        services.AddSingleton<DocumentScannerService>();
        services.AddSingleton<FileImportService>();
        services.AddSingleton<PhotoLibraryImportService>();

        // VaultService is singleton — holds the in-memory master key
        services.AddSingleton<VaultService>();

        // ── Scoped (per OCR session) ──────────────────────────────────────────
        services.AddScoped<OcrSyncPreparationService>();
        services.AddScoped<MedicalHistoryAutoAppendService>();
        services.AddScoped<OcrSessionViewModel>();
        services.AddScoped<SecurityPostureViewModel>();

        return services;
    }
}
