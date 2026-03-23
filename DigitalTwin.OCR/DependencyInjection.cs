using DigitalTwin.OCR.Services;
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

        services.AddSingleton<DocumentEncryptionService>();
        services.AddSingleton<HashingService>();
        services.AddSingleton<SensitiveDataSanitizer>();

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
        services.AddScoped<OcrSessionViewModel>();
        services.AddScoped<SecurityPostureViewModel>();

        return services;
    }
}
