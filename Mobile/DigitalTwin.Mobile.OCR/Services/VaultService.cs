using System.Text.Json;
using DigitalTwin.Mobile.OCR.Models;
using DigitalTwin.Mobile.OCR.Models.Enums;
using DigitalTwin.Mobile.OCR.Policies;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.OCR.Services;

/// <summary>
/// Manages the local AES-GCM encrypted vault.
/// Vault root: received via constructor (Swift passes Library/Application Support/OCRVault/).
/// Subfolders: quarantine/, encrypted/, manifests/, temp/
/// Master key: received as byte[] from Swift (Keychain access is on the Swift side).
/// </summary>
public sealed class VaultService
{
    private readonly ILogger<VaultService> _logger;
    private readonly string _vaultRoot;
    private bool _isUnlocked;
    private byte[]? _masterKey;

    public bool IsInitialized => Directory.Exists(VaultPath("encrypted"));
    public bool IsUnlocked => _isUnlocked;

    public VaultService(string vaultRoot, ILogger<VaultService> logger)
    {
        _vaultRoot = vaultRoot;
        _logger = logger;
    }

    public OcrResult<bool> Initialize(SecurityPosture posture)
    {
        if (!DocumentSecurityPolicy.CanInitializeVault(posture))
            return OcrResult<bool>.Fail("Device passcode is required to initialize the vault in Strict mode.");

        try
        {
            foreach (var sub in new[] { "quarantine", "encrypted", "manifests", "temp" })
                Directory.CreateDirectory(VaultPath(sub));

            _logger.LogInformation("[OCR Vault] Initialized at opaque path.");
            return OcrResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR Vault] Initialization failed.");
            return OcrResult<bool>.Fail("Vault initialization failed.");
        }
    }

    /// <summary>
    /// Unlocks the vault with the master key retrieved by Swift from Keychain.
    /// </summary>
    public OcrResult<bool> Unlock(byte[] masterKey)
    {
        var (valid, reason) = DocumentSecurityPolicy.ValidateMasterKey(masterKey);
        if (!valid)
            return OcrResult<bool>.Fail(reason!);

        _masterKey = masterKey;
        _isUnlocked = true;
        _logger.LogInformation("[OCR Vault] Unlocked.");
        return OcrResult<bool>.Ok(true);
    }

    public void Lock()
    {
        if (_masterKey is not null)
        {
            Array.Clear(_masterKey, 0, _masterKey.Length);
            _masterKey = null;
        }
        _isUnlocked = false;
    }

    public async Task<OcrResult<EncryptedDocumentDescriptor>> StoreDocumentAsync(
        byte[] normalizedBytes, string mimeType, int pageCount, Guid documentId)
    {
        if (!_isUnlocked || _masterKey is null)
            return OcrResult<EncryptedDocumentDescriptor>.Fail("Vault is locked.");

        try
        {
            EnsureVaultDirectoriesExist();

            var sha256 = HashingService.ComputeSha256Hex(normalizedBytes);
            var payload = DocumentEncryptionService.Encrypt(normalizedBytes, _masterKey, documentId, mimeType, pageCount, sha256);

            var encPath = VaultPath($"encrypted/{documentId:N}.enc");
            await File.WriteAllBytesAsync(encPath, payload.Ciphertext);

            var descriptor = payload.Descriptor with { VaultPath = encPath };
            var manifestPath = VaultPath($"manifests/{documentId:N}.json");
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(descriptor, OcrJsonContext.Default.EncryptedDocumentDescriptor));

            _logger.LogInformation("[OCR Vault] Stored document {Ref}.", LoggingRedactionPolicy.SafeDocumentRef(documentId));
            return OcrResult<EncryptedDocumentDescriptor>.Ok(descriptor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR Vault] StoreDocumentAsync failed.");
            return OcrResult<EncryptedDocumentDescriptor>.Fail("Failed to store document in vault.");
        }
    }

    public async Task<OcrResult<byte[]>> RetrieveDocumentAsync(Guid documentId)
    {
        if (!_isUnlocked || _masterKey is null)
            return OcrResult<byte[]>.Fail("Vault is locked.");

        try
        {
            var manifestPath = VaultPath($"manifests/{documentId:N}.json");
            if (!File.Exists(manifestPath))
                return OcrResult<byte[]>.Fail("Manifest not found.");

            var descriptor = JsonSerializer.Deserialize(
                await File.ReadAllTextAsync(manifestPath), OcrJsonContext.Default.EncryptedDocumentDescriptor);

            if (descriptor is null)
                return OcrResult<byte[]>.Fail("Manifest is corrupt.");

            if (string.IsNullOrWhiteSpace(descriptor.VaultPath))
                return OcrResult<byte[]>.Fail("Manifest is missing vault path.");

            var cipherPath = descriptor.VaultPath;
            if (!File.Exists(cipherPath))
            {
                var healed = HealVaultPath(cipherPath);
                if (!string.IsNullOrWhiteSpace(healed) && File.Exists(healed))
                    cipherPath = healed;
            }

            if (!File.Exists(cipherPath))
                return OcrResult<byte[]>.Fail("Encrypted file missing (vault metadata out of sync).");

            var ciphertext = await File.ReadAllBytesAsync(cipherPath);
            var plaintext = DocumentEncryptionService.Decrypt(ciphertext, descriptor, _masterKey);
            return OcrResult<byte[]>.Ok(plaintext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR Vault] RetrieveDocumentAsync failed for {Ref}.",
                LoggingRedactionPolicy.SafeDocumentRef(documentId));
            return OcrResult<byte[]>.Fail("Failed to retrieve document from vault.");
        }
    }

    public async Task<OcrResult<EncryptedDocumentDescriptor>> GetDescriptorAsync(Guid documentId)
    {
        try
        {
            var manifestPath = VaultPath($"manifests/{documentId:N}.json");
            if (!File.Exists(manifestPath))
                return OcrResult<EncryptedDocumentDescriptor>.Fail("Manifest not found.");

            var descriptor = JsonSerializer.Deserialize(
                await File.ReadAllTextAsync(manifestPath), OcrJsonContext.Default.EncryptedDocumentDescriptor);

            return descriptor is null
                ? OcrResult<EncryptedDocumentDescriptor>.Fail("Manifest is corrupt.")
                : OcrResult<EncryptedDocumentDescriptor>.Ok(descriptor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR Vault] GetDescriptorAsync failed.");
            return OcrResult<EncryptedDocumentDescriptor>.Fail("Failed to read manifest.");
        }
    }

    public OcrResult<bool> DeleteDocument(Guid documentId)
    {
        try
        {
            var encPath = VaultPath($"encrypted/{documentId:N}.enc");
            var manifestPath = VaultPath($"manifests/{documentId:N}.json");

            if (File.Exists(encPath)) File.Delete(encPath);
            if (File.Exists(manifestPath)) File.Delete(manifestPath);

            return OcrResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR Vault] DeleteDocument failed.");
            return OcrResult<bool>.Fail("Failed to delete document from vault.");
        }
    }

    public void Wipe()
    {
        Lock();
        try
        {
            if (Directory.Exists(_vaultRoot))
                Directory.Delete(_vaultRoot, recursive: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR Vault] Wipe failed.");
        }
    }

    public string QuarantinePath(string opaqueName) => VaultPath($"quarantine/{opaqueName}");
    public string TempPath(string opaqueName) => VaultPath($"temp/{opaqueName}");

    private string VaultPath(string relative) => Path.Combine(_vaultRoot, relative);

    /// <summary>
    /// Ensures vault subfolders exist (e.g. after app reinstall where Keychain remains but filesystem was wiped,
    /// or if Initialize was skipped while Unlock succeeded).
    /// </summary>
    private void EnsureVaultDirectoriesExist()
    {
        foreach (var sub in new[] { "quarantine", "encrypted", "manifests", "temp" })
            Directory.CreateDirectory(VaultPath(sub));
    }

    private static string? HealVaultPath(string path)
    {
        var broken = "/Library/Library/Application Support/";
        var healed = "/Library/Application Support/";

        if (path.Contains(broken, StringComparison.Ordinal))
            return path.Replace(broken, healed, StringComparison.Ordinal);

        return null;
    }
}
