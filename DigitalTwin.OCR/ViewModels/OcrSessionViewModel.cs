using DigitalTwin.OCR.Models;
using DigitalTwin.OCR.Models.Enums;
using DigitalTwin.OCR.Policies;
using DigitalTwin.OCR.Services;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.ViewModels;

/// <summary>
/// Orchestrates the full OCR session:
/// scan/import → quarantine → validate → normalize → encrypt → vault → OCR → sanitize → persist
/// </summary>
public sealed class OcrSessionViewModel
{
    private const string CancelledMarker = "cancelled";
    private readonly DocumentScannerService _scanner;
    private readonly FileImportService _fileImport;
    private readonly PhotoLibraryImportService _photoLibrary;
    private readonly DocumentNormalizationService _normalizer;
    private readonly DocumentEncryptionService _encryption;
    private readonly VaultService _vault;
    private readonly LocalAuthenticationService _localAuth;
    private readonly LocalOcrService _ocr;
    private readonly SensitiveDataSanitizer _sanitizer;
    private readonly OcrSyncPreparationService _syncPrep;
    private readonly MedicalHistoryAutoAppendService _historyAutoAppend;
    private readonly OcrSheetService _sheetService;
    private readonly OcrOptions _options;
    private readonly ILogger<OcrSessionViewModel> _logger;

    // ── Observable state ──────────────────────────────────────────────────────
    public bool IsLoading { get; private set; }
    public string? StatusMessage { get; private set; }
    public OcrExtractionResult? OcrResult { get; private set; }
    public string? SanitizedPreview { get; private set; }
    public OcrDocumentSyncRecord? SavedRecord { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Action? StateChanged { get; set; }

    public OcrSessionViewModel(
        DocumentScannerService scanner,
        FileImportService fileImport,
        PhotoLibraryImportService photoLibrary,
        DocumentNormalizationService normalizer,
        DocumentEncryptionService encryption,
        VaultService vault,
        LocalAuthenticationService localAuth,
        LocalOcrService ocr,
        SensitiveDataSanitizer sanitizer,
        OcrSyncPreparationService syncPrep,
        MedicalHistoryAutoAppendService historyAutoAppend,
        OcrSheetService sheetService,
        OcrOptions options,
        ILogger<OcrSessionViewModel> logger)
    {
        _scanner = scanner;
        _fileImport = fileImport;
        _photoLibrary = photoLibrary;
        _normalizer = normalizer;
        _encryption = encryption;
        _vault = vault;
        _localAuth = localAuth;
        _ocr = ocr;
        _sanitizer = sanitizer;
        _syncPrep = syncPrep;
        _historyAutoAppend = historyAutoAppend;
        _sheetService = sheetService;
        _options = options;
        _logger = logger;
    }

    /// <summary>Initializes the vault for the first time (creates directories + keychain entry).</summary>
    public Task<OcrResult<bool>> InitVaultAsync(CancellationToken ct = default)
    {
        SetLoading("Initializing vault…");
        var posture = _vault.IsInitialized
            ? new Models.SecurityPosture(true, false, "None", true, false, _options.SecurityMode)
            : new Models.SecurityPosture(true, false, "None", false, false, _options.SecurityMode);

        var result = _vault.Initialize(posture);
        if (!result.IsSuccess)
            SetError(result.Error!);
        else
            ClearLoading();

        return Task.FromResult(result);
    }

    /// <summary>Unlocks the vault (biometric prompt) and updates posture.</summary>
    public async Task<OcrResult<bool>> UnlockVaultAsync(CancellationToken ct = default)
    {
        SetLoading("Authenticating…");

        var authResult = await _localAuth.AuthenticateAsync("Unlock your OCR vault", ct);
        if (!authResult.IsSuccess)
        {
            SetError(authResult.Error!);
            return OcrResult<bool>.Fail(authResult.Error!);
        }

        var unlockResult = _vault.Unlock();
        if (!unlockResult.IsSuccess)
        {
            SetError(unlockResult.Error!);
            return OcrResult<bool>.Fail(unlockResult.Error!);
        }

        ClearLoading();
        return OcrResult<bool>.Ok(true);
    }

    /// <summary>Full pipeline: scan via camera.</summary>
    public async Task RunCameraSessionAsync(Guid patientId, CancellationToken ct = default)
    {
        ResetSessionState();
        SetLoading("Opening camera…");
        var scanResult = await _scanner.ScanAsync(ct);
        if (!scanResult.IsSuccess)
        {
            HandleFailure(scanResult.Error);
            return;
        }

        await ProcessQuarantineFilesAsync(scanResult.Value!, DocumentMimeType.Jpeg, patientId, ct);
    }

    /// <summary>Full pipeline: import from file picker (PDF / images from Files).</summary>
    public async Task RunFileImportSessionAsync(Guid patientId, CancellationToken ct = default)
    {
        ResetSessionState();
        SetLoading("Selecting file…");
        var pickResult = await _fileImport.PickAndImportAsync(ct);
        if (!pickResult.IsSuccess)
        {
            HandleFailure(pickResult.Error);
            return;
        }

        var (quarantinePath, mimeType) = pickResult.Value!;
        await ProcessQuarantineFileAsync(quarantinePath, mimeType, patientId, ct);
    }

    /// <summary>Full pipeline: pick an image from the Photos library (PHPicker), then same pipeline as file import.</summary>
    public async Task RunPhotoLibraryImportSessionAsync(Guid patientId, CancellationToken ct = default)
    {
        ResetSessionState();
        SetLoading("Opening Photos…");
        var pickResult = await _photoLibrary.PickAndImportAsync(ct);
        if (!pickResult.IsSuccess)
        {
            HandleFailure(pickResult.Error);
            return;
        }

        var (quarantinePath, mimeType) = pickResult.Value!;
        await ProcessQuarantineFileAsync(quarantinePath, mimeType, patientId, ct);
    }

    private async Task ProcessQuarantineFilesAsync(
        IReadOnlyList<string> paths, DocumentMimeType mimeType, Guid patientId, CancellationToken ct)
    {
        // For multi-page camera scans, treat all pages as one document
        if (paths.Count == 0) { SetError("No pages scanned."); return; }
        await ProcessQuarantineFileAsync(paths[0], mimeType, patientId, ct);
    }

    private async Task ProcessQuarantineFileAsync(
        string quarantinePath, DocumentMimeType mimeType, Guid patientId, CancellationToken ct)
    {
        var documentId = Guid.NewGuid();
        string? cleanQuarantinePath = quarantinePath;

        try
        {
            // 1 — Normalise
            SetLoading("Normalizing document…");
            var normalizeResult = await _normalizer.NormalizeAsync(quarantinePath, mimeType);
            if (!normalizeResult.IsSuccess) { SetError(normalizeResult.Error!); return; }

            var (normalized, pageCount, normMime) = normalizeResult.Value!;

            // 2 — Compute hash
            var sha256 = HashingService.ComputeSha256Hex(normalized);

            // 3 — Encrypt into vault
            SetLoading("Encrypting…");
            var storeResult = await _vault.StoreDocumentAsync(normalized, normMime, pageCount, documentId);
            if (!storeResult.IsSuccess) { SetError(storeResult.Error!); return; }

            // 4 — OCR (operates on the normalized plaintext)
            SetLoading("Running OCR…");
            var ocrResult = await _ocr.RunOcrAsync(quarantinePath, mimeType, _options.UseAccurateOcr, ct);
            if (!ocrResult.IsSuccess) { SetError(ocrResult.Error!); return; }
            OcrResult = ocrResult.Value;

            // 5 — Sanitize
            SetLoading("Sanitizing…");
            var pageTexts = ocrResult.Value!.Pages.Select(p => p.Blocks.Select(b => b.Text).StringJoin("\n"));
            SanitizedPreview = _sanitizer.BuildSanitizedPreview(pageTexts, _options.MaxSanitizedPreviewLength);

            // 6 — Persist sync record
            SetLoading("Saving…");
            var record = new OcrDocumentSyncRecord(
                documentId,
                patientId,
                storeResult.Value!.OpaqueInternalName,
                storeResult.Value.MimeType,
                storeResult.Value.PageCount,
                sha256,
                SanitizedPreview,
                storeResult.Value.VaultPath,
                DateTime.UtcNow);

            var saveResult = await _syncPrep.SaveAsync(record, ct);
            if (!saveResult.IsSuccess)
                _logger.LogWarning("[OCR VM] Sync record save failed: {Msg}", saveResult.Error);
            else
                await _historyAutoAppend.AppendAsync(patientId, record.Id, SanitizedPreview, ct);

            SavedRecord = record;
            ClearLoading();
        }
        finally
        {
            // Wipe quarantine immediately — plaintext never stays on disk
            if (cleanQuarantinePath is not null && File.Exists(cleanQuarantinePath))
                File.Delete(cleanQuarantinePath);
        }
    }

    public void CompleteSession()
    {
        var result = SavedRecord is not null
            ? new OcrSessionResult(
                OcrSessionStatus.Success,
                SavedRecord.Id,
                SanitizedPreview,
                OcrResult?.OverallStatus ?? OcrExecutionStatus.Success)
            : new OcrSessionResult(OcrSessionStatus.Cancelled);

        _sheetService.Complete(result);
    }

    /// <summary>
    /// Clears transient UI/session state so the user can retry import/scan after cancellation or errors.
    /// </summary>
    public void ResetSessionState()
    {
        IsLoading = false;
        StatusMessage = null;
        ErrorMessage = null;
        OcrResult = null;
        SanitizedPreview = null;
        SavedRecord = null;
        StateChanged?.Invoke();
    }

    private void SetLoading(string message)
    {
        IsLoading = true;
        StatusMessage = message;
        ErrorMessage = null;
        StateChanged?.Invoke();
    }

    private void SetError(string message)
    {
        IsLoading = false;
        ErrorMessage = message;
        StatusMessage = null;
        StateChanged?.Invoke();
    }

    private void ClearLoading()
    {
        IsLoading = false;
        StatusMessage = null;
        StateChanged?.Invoke();
    }

    private void HandleFailure(string? error)
    {
        if (IsUserCancellation(error))
        {
            IsLoading = false;
            StatusMessage = null;
            ErrorMessage = "Upload cancelled. You can try again.";
            StateChanged?.Invoke();
            return;
        }

        SetError(error ?? "Operation failed.");
    }

    private static bool IsUserCancellation(string? error)
        => !string.IsNullOrWhiteSpace(error)
           && error.Contains(CancelledMarker, StringComparison.OrdinalIgnoreCase);
}

internal static class StringExtensions
{
    public static string StringJoin(this IEnumerable<string> values, string separator)
        => string.Join(separator, values);
}
