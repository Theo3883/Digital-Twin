using System.Security.Cryptography;
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

            _logger.LogDebug("[OCR Vault] Created subdirectories: quarantine, encrypted, manifests, temp");
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

        _logger.LogDebug("[OCR Vault] Unlock requested: keyLen={KeyLen}, initialized={Initialized}, isUnlocked={IsUnlocked}, manifestCount={ManifestCount}",
            masterKey.Length, IsInitialized, _isUnlocked, GetManifestCount());

        var keyValidation = ValidateMasterKeyAgainstExistingData(masterKey);
        if (!keyValidation.IsSuccess)
        {
            _logger.LogWarning("[OCR Vault] Unlock rejected: {Reason}", keyValidation.Error);
            return OcrResult<bool>.Fail(keyValidation.Error!);
        }

        _masterKey = [.. masterKey];
        _isUnlocked = true;
        _logger.LogDebug("[OCR Vault] Unlock: master key length={KeyLen} bytes, keyFingerprint={Fingerprint}",
            masterKey.Length, KeyFingerprint(masterKey));
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
            _logger.LogDebug("[OCR Vault] StoreDocument: docId={DocId}, mime={Mime}, pages={Pages}, inputBytes={Size}, sha256={Hash}",
                documentId, mimeType, pageCount, normalizedBytes.Length, sha256[..Math.Min(16, sha256.Length)]);
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

            _logger.LogDebug("[OCR Vault] Retrieve manifest: docId={DocId}, nonceLen={NonceLen}, tagLen={TagLen}, wrappedLen={WrappedLen}",
                documentId,
                descriptor.NonceB64?.Length ?? 0,
                descriptor.TagB64?.Length ?? 0,
                descriptor.WrappedDekB64?.Length ?? 0);

            var cipherPath = ResolveCipherPath(descriptor, out var resolutionDetail);
            if (string.IsNullOrWhiteSpace(cipherPath))
            {
                _logger.LogWarning("[OCR Vault] Retrieve: cipher path unresolved for {Ref}. {Detail}",
                    LoggingRedactionPolicy.SafeDocumentRef(documentId), resolutionDetail);
                return OcrResult<byte[]>.Fail("Encrypted file missing (vault metadata out of sync).");
            }

            if (!string.Equals(descriptor.VaultPath, cipherPath, StringComparison.Ordinal))
            {
                _logger.LogInformation("[OCR Vault] Retrieve: resolved cipher path via fallback for {Ref}. {Detail}",
                    LoggingRedactionPolicy.SafeDocumentRef(documentId), resolutionDetail);
                descriptor = descriptor with { VaultPath = cipherPath };
                await TryPersistHealedManifestPathAsync(manifestPath, descriptor);
            }

            var ciphertext = await File.ReadAllBytesAsync(cipherPath);
            _logger.LogDebug("[OCR Vault] Retrieve: docId={DocId}, cipherSize={Size} bytes",
                documentId, ciphertext.Length);
            var plaintext = DocumentEncryptionService.Decrypt(ciphertext, descriptor, _masterKey);
            _logger.LogDebug("[OCR Vault] Retrieve: decrypted {Size} bytes for {Ref}",
                plaintext.Length, LoggingRedactionPolicy.SafeDocumentRef(documentId));
            return OcrResult<byte[]>.Ok(plaintext);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "[OCR Vault] RetrieveDocumentAsync crypto failure for {Ref}. keyFingerprint={Fingerprint}",
                LoggingRedactionPolicy.SafeDocumentRef(documentId), KeyFingerprint(_masterKey));
            return OcrResult<byte[]>.Fail("Failed to decrypt document. Vault key mismatch.");
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

            _logger.LogDebug("[OCR Vault] Delete: enc exists={EncExists}, manifest exists={ManExists}",
                File.Exists(encPath), File.Exists(manifestPath));
            if (File.Exists(encPath)) File.Delete(encPath);
            if (File.Exists(manifestPath)) File.Delete(manifestPath);

            _logger.LogInformation("[OCR Vault] Deleted vault files for {Ref}", LoggingRedactionPolicy.SafeDocumentRef(documentId));
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

    private string? ResolveCipherPath(EncryptedDocumentDescriptor descriptor, out string resolutionDetail)
    {
        var candidates = BuildCipherPathCandidates(descriptor);

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                resolutionDetail = string.Equals(candidate, descriptor.VaultPath, StringComparison.Ordinal)
                    ? "source=manifest"
                    : $"source=fallback file={Path.GetFileName(candidate)}";
                return candidate;
            }
        }

        resolutionDetail = $"tried={string.Join(" | ", candidates.Select(Path.GetFileName))}";
        return null;
    }

    private string[] BuildCipherPathCandidates(EncryptedDocumentDescriptor descriptor)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(descriptor.VaultPath))
            candidates.Add(descriptor.VaultPath);

        var healedPath = string.IsNullOrWhiteSpace(descriptor.VaultPath)
            ? null
            : HealVaultPath(descriptor.VaultPath);
        if (!string.IsNullOrWhiteSpace(healedPath))
            candidates.Add(healedPath);

        if (!string.IsNullOrWhiteSpace(descriptor.VaultPath))
        {
            var fileName = Path.GetFileName(descriptor.VaultPath);
            if (!string.IsNullOrWhiteSpace(fileName))
                candidates.Add(VaultPath(Path.Combine("encrypted", fileName)));
        }

        if (!string.IsNullOrWhiteSpace(descriptor.OpaqueInternalName))
        {
            var opaqueFile = descriptor.OpaqueInternalName.EndsWith(".enc", StringComparison.OrdinalIgnoreCase)
                ? descriptor.OpaqueInternalName
                : $"{descriptor.OpaqueInternalName}.enc";
            candidates.Add(VaultPath(Path.Combine("encrypted", opaqueFile)));
        }

        candidates.Add(VaultPath($"encrypted/{descriptor.DocumentId:N}.enc"));

        return candidates
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private async Task TryPersistHealedManifestPathAsync(string manifestPath, EncryptedDocumentDescriptor descriptor)
    {
        try
        {
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(descriptor, OcrJsonContext.Default.EncryptedDocumentDescriptor));

            _logger.LogInformation("[OCR Vault] Persisted healed manifest path for {Ref}.",
                LoggingRedactionPolicy.SafeDocumentRef(descriptor.DocumentId));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[OCR Vault] Failed to persist healed manifest path for {Ref}.",
                LoggingRedactionPolicy.SafeDocumentRef(descriptor.DocumentId));
        }
    }

    private OcrResult<bool> ValidateMasterKeyAgainstExistingData(byte[] masterKey)
    {
        try
        {
            var manifestsDir = VaultPath("manifests");
            if (!Directory.Exists(manifestsDir))
                return OcrResult<bool>.Ok(true);

            var manifests = Directory.EnumerateFiles(manifestsDir, "*.json").Take(3).ToArray();
            if (manifests.Length == 0)
                return OcrResult<bool>.Ok(true);

            foreach (var manifestPath in manifests)
            {
                try
                {
                    var json = File.ReadAllText(manifestPath);
                    var descriptor = JsonSerializer.Deserialize(json, OcrJsonContext.Default.EncryptedDocumentDescriptor);
                    if (descriptor is null)
                    {
                        _logger.LogDebug("[OCR Vault] Unlock validation skipped manifest {Manifest}: corrupt descriptor",
                            Path.GetFileName(manifestPath));
                        continue;
                    }

                    var cipherPath = ResolveCipherPath(descriptor, out var resolutionDetail);
                    if (string.IsNullOrWhiteSpace(cipherPath))
                    {
                        _logger.LogDebug("[OCR Vault] Unlock validation skipped manifest {Manifest}: cipher path missing. {Detail}",
                            Path.GetFileName(manifestPath), resolutionDetail);
                        continue;
                    }

                    if (!string.Equals(descriptor.VaultPath, cipherPath, StringComparison.Ordinal))
                    {
                        _logger.LogDebug("[OCR Vault] Unlock validation resolved cipher path via fallback for manifest {Manifest}. {Detail}",
                            Path.GetFileName(manifestPath), resolutionDetail);
                    }

                    var ciphertext = File.ReadAllBytes(cipherPath);
                    _ = DocumentEncryptionService.Decrypt(ciphertext, descriptor, masterKey);

                    _logger.LogDebug("[OCR Vault] Unlock validation succeeded using manifest {Manifest}",
                        Path.GetFileName(manifestPath));
                    return OcrResult<bool>.Ok(true);
                }
                catch (CryptographicException ex)
                {
                    _logger.LogWarning(ex, "[OCR Vault] Unlock validation key mismatch on manifest {Manifest}",
                        Path.GetFileName(manifestPath));
                    return OcrResult<bool>.Fail("Master key does not match existing vault data.");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[OCR Vault] Unlock validation ignored manifest {Manifest}",
                        Path.GetFileName(manifestPath));
                }
            }

            _logger.LogDebug("[OCR Vault] Unlock validation found manifests but none usable for key verification; proceeding");
            return OcrResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[OCR Vault] Unlock validation failed unexpectedly; proceeding without strict key check");
            return OcrResult<bool>.Ok(true);
        }
    }

    private int GetManifestCount()
    {
        try
        {
            var manifestsDir = VaultPath("manifests");
            if (!Directory.Exists(manifestsDir))
                return 0;

            return Directory.EnumerateFiles(manifestsDir, "*.json").Count();
        }
        catch
        {
            return -1;
        }
    }

    private static string KeyFingerprint(ReadOnlySpan<byte> key)
    {
        var hash = SHA256.HashData(key);
        return Convert.ToHexString(hash[..4]);
    }
}
