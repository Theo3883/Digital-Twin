using System.Text.Json;
using DigitalTwin.OCR.Models;
using DigitalTwin.OCR.Models.Enums;
using DigitalTwin.OCR.Policies;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Manages the local AES-GCM encrypted vault.
/// Vault root: {AppDataDirectory}/Library/Application Support/DigitalTwin.OcrVault/
/// Subfolders: quarantine/, encrypted/, manifests/, temp/
/// </summary>
public sealed class VaultService
{
    private readonly DocumentEncryptionService _encryption;
    private readonly KeychainKeyStore _keychain;
    private readonly FileProtectionService _fileProtection;
    private readonly ILogger<VaultService> _logger;
    private readonly string _vaultRoot;
    private bool _isUnlocked;
    private byte[]? _masterKey;

    public bool IsInitialized => Directory.Exists(VaultPath("encrypted")) && _keychain.KeyExists();
    public bool IsUnlocked => _isUnlocked;

    public VaultService(
        DocumentEncryptionService encryption,
        KeychainKeyStore keychain,
        FileProtectionService fileProtection,
        ILogger<VaultService> logger)
    {
        _encryption = encryption;
        _keychain = keychain;
        _fileProtection = fileProtection;
        _logger = logger;

        _vaultRoot = Path.Combine(
            FileSystem.AppDataDirectory,
            "Library", "Application Support", "DigitalTwin.OcrVault");
    }

    public OcrResult<bool> Initialize(SecurityPosture posture)
    {
        var (canInit, reason) = (true, (string?)null);
        if (!DocumentSecurityPolicy.CanInitializeVault(posture))
            return OcrResult<bool>.Fail("Device passcode is required to initialize the vault in Strict mode.");

        try
        {
            foreach (var sub in new[] { "quarantine", "encrypted", "manifests", "temp" })
            {
                var path = VaultPath(sub);
                Directory.CreateDirectory(path);
                _fileProtection.ExcludeFromBackup(path);
            }
            _fileProtection.ExcludeFromBackup(_vaultRoot);

            if (!_keychain.KeyExists())
            {
                var newKey = HashingService.GenerateKey256();
                var storeResult = _keychain.StoreKey(newKey);
                if (!storeResult.IsSuccess)
                    return OcrResult<bool>.Fail($"Could not store master key: {storeResult.Error}");
            }

            _logger.LogInformation("[OCR Vault] Initialized at opaque path.");
            return OcrResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR Vault] Initialization failed.");
            return OcrResult<bool>.Fail("Vault initialization failed.");
        }
    }

    public OcrResult<bool> Unlock()
    {
        var keyResult = _keychain.RetrieveKey();
        if (!keyResult.IsSuccess)
            return OcrResult<bool>.Fail(keyResult.Error!);

        _masterKey = keyResult.Value;
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
            var sha256 = HashingService.ComputeSha256Hex(normalizedBytes);
            var payload = _encryption.Encrypt(normalizedBytes, _masterKey, documentId, mimeType, pageCount, sha256);

            var encPath = VaultPath($"encrypted/{documentId:N}.enc");
            await File.WriteAllBytesAsync(encPath, payload.Ciphertext);
            _fileProtection.ApplyCompleteProtection(encPath);

            var descriptor = payload.Descriptor with { VaultPath = encPath };
            var manifestPath = VaultPath($"manifests/{documentId:N}.json");
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(descriptor));
            _fileProtection.ApplyCompleteProtection(manifestPath);

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

            var descriptor = JsonSerializer.Deserialize<EncryptedDocumentDescriptor>(
                await File.ReadAllTextAsync(manifestPath));

            if (descriptor is null)
                return OcrResult<byte[]>.Fail("Manifest is corrupt.");

            if (string.IsNullOrWhiteSpace(descriptor.VaultPath))
                return OcrResult<byte[]>.Fail("Manifest is missing vault path.");

            var cipherPath = descriptor.VaultPath;
            if (!File.Exists(cipherPath))
            {
                // Compatibility: older builds can persist paths that include duplicated
                // ".../Library/Library/Application Support/..." segments on iOS simulators.
                // If the healed path exists, use it instead of failing hard.
                var healed = HealVaultPath(cipherPath);
                if (!string.IsNullOrWhiteSpace(healed) && File.Exists(healed))
                    cipherPath = healed;
            }

            if (!File.Exists(cipherPath))
                return OcrResult<byte[]>.Fail("Encrypted file missing (vault metadata out of sync).");

            var ciphertext = await File.ReadAllBytesAsync(cipherPath);
            var plaintext = _encryption.Decrypt(ciphertext, descriptor, _masterKey);
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

            var descriptor = JsonSerializer.Deserialize<EncryptedDocumentDescriptor>(
                await File.ReadAllTextAsync(manifestPath));

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
            _keychain.DeleteKey();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR Vault] Wipe failed.");
        }
    }

    public string QuarantinePath(string opaqueName) => VaultPath($"quarantine/{opaqueName}");
    public string TempPath(string opaqueName) => VaultPath($"temp/{opaqueName}");

    private string VaultPath(string relative) => Path.Combine(_vaultRoot, relative);

    private static string? HealVaultPath(string path)
    {
        // Example broken path:
        // /.../Library/Library/Application Support/DigitalTwin.OcrVault/encrypted/<id>.enc
        // Example healed path:
        // /.../Library/Application Support/DigitalTwin.OcrVault/encrypted/<id>.enc
        var broken = "/Library/Library/Application Support/";
        var healed = "/Library/Application Support/";

        if (path.Contains(broken, StringComparison.Ordinal))
            return path.Replace(broken, healed, StringComparison.Ordinal);

        return null;
    }
}
