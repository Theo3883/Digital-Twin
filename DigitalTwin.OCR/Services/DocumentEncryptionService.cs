using System.Security.Cryptography;
using DigitalTwin.OCR.Models;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// AES-GCM-256 document encryption.
/// BCL only — System.Security.Cryptography.AesGcm, no third-party packages.
/// Each document gets a unique DEK; the DEK is wrapped with the master key from the Keychain.
/// </summary>
public sealed class DocumentEncryptionService
{
    private const int NonceSize = 12;   // 96-bit nonce
    private const int TagSize = 16;     // 128-bit authentication tag
    private const int KeySize = 32;     // 256-bit key

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> using a freshly generated DEK wrapped with <paramref name="masterKey"/>.
    /// Returns a descriptor containing the nonce, tag, and wrapped DEK (all base64-encoded) suitable for the manifest.
    /// </summary>
    public EncryptedPayload Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> masterKey, Guid documentId, string mimeType, int pageCount, string sha256)
    {
        var dek = HashingService.GenerateKey256();
        var nonce = HashingService.GenerateNonce96();
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(dek, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var wrappedDek = WrapKey(dek, masterKey);

        return new EncryptedPayload(
            Ciphertext: ciphertext,
            Descriptor: new EncryptedDocumentDescriptor(
                DocumentId: documentId,
                OpaqueInternalName: $"{documentId:N}",
                VaultPath: string.Empty, // caller sets this after writing to disk
                NonceB64: Convert.ToBase64String(nonce),
                TagB64: Convert.ToBase64String(tag),
                WrappedDekB64: Convert.ToBase64String(wrappedDek),
                Sha256OfNormalized: sha256,
                MimeType: mimeType,
                PageCount: pageCount,
                EncryptedAt: DateTime.UtcNow));
    }

    /// <summary>Decrypts a vault payload using the wrapped DEK and master key.</summary>
    public byte[] Decrypt(ReadOnlySpan<byte> ciphertext, EncryptedDocumentDescriptor descriptor, ReadOnlySpan<byte> masterKey)
    {
        var nonce = Convert.FromBase64String(descriptor.NonceB64);
        var tag = Convert.FromBase64String(descriptor.TagB64);
        var wrappedDek = Convert.FromBase64String(descriptor.WrappedDekB64);

        var dek = UnwrapKey(wrappedDek, masterKey);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(dek, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }

    // ── Key wrapping via AES-GCM (master-key wraps the DEK) ──────────────────

    private static byte[] WrapKey(byte[] dek, ReadOnlySpan<byte> masterKey)
    {
        var nonce = HashingService.GenerateNonce96();
        var wrapped = new byte[dek.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(masterKey, TagSize);
        aes.Encrypt(nonce, dek, wrapped, tag);

        // Layout: nonce(12) + tag(16) + wrapped(32) = 60 bytes
        return [.. nonce, .. tag, .. wrapped];
    }

    private static byte[] UnwrapKey(byte[] wrappedBundle, ReadOnlySpan<byte> masterKey)
    {
        var nonce = wrappedBundle[..NonceSize];
        var tag = wrappedBundle[NonceSize..(NonceSize + TagSize)];
        var wrapped = wrappedBundle[(NonceSize + TagSize)..];
        var dek = new byte[wrapped.Length];

        using var aes = new AesGcm(masterKey, TagSize);
        aes.Decrypt(nonce, wrapped, tag, dek);
        return dek;
    }
}

public record EncryptedPayload(byte[] Ciphertext, EncryptedDocumentDescriptor Descriptor);
