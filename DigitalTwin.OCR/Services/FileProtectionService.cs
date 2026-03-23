using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Applies NSFileProtectionComplete to vault files, locking them when the device is locked.
/// </summary>
public sealed class FileProtectionService
{
    private readonly ILogger<FileProtectionService> _logger;

    public FileProtectionService(ILogger<FileProtectionService> logger) => _logger = logger;

#if IOS || MACCATALYST
    public void ApplyCompleteProtection(string filePath)
    {
        try
        {
            var url = Foundation.NSUrl.FromFilename(filePath);
            url.SetResource(Foundation.NSUrl.FileProtectionKey, Foundation.NSUrl.FileProtectionComplete);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR FileProtection] Exception applying file protection to vault-file.");
        }
    }

    public void ExcludeFromBackup(string filePath)
    {
        try
        {
            var url = Foundation.NSUrl.FromFilename(filePath);
            url.SetResource(Foundation.NSUrl.IsExcludedFromBackupKey, Foundation.NSNumber.FromBoolean(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR FileProtection] Exception excluding from backup.");
        }
    }
#else
    public void ApplyCompleteProtection(string filePath) { }
    public void ExcludeFromBackup(string filePath) { }
#endif
}
