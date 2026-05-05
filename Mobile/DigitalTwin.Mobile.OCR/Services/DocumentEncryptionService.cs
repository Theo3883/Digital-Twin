using System.Security.Cryptography;
using DigitalTwin.Mobile.OCR.Models;

namespace DigitalTwin.Mobile.OCR.Services;

/// <summary>
/// AES-GCM-256 document encryption.
/// BCL only — System.Security.Cryptography.AesGcm, no third-party packages.
/// Each document gets a unique DEK; the DEK is wrapped with the master key from the Keychain.
/// </summary>
public sealed class DocumentEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static EncryptedPayload Encrypt(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> masterKey,
        Guid documentId,
        string mimeType,
        int pageCount,
        string sha256)
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
                VaultPath: string.Empty,
                NonceB64: Convert.ToBase64String(nonce),
                TagB64: Convert.ToBase64String(tag),
                WrappedDekB64: Convert.ToBase64String(wrappedDek),
                Sha256OfNormalized: sha256,
                MimeType: mimeType,
                PageCount: pageCount,
                EncryptedAt: DateTime.UtcNow));
    }

    public static byte[] Decrypt(
        ReadOnlySpan<byte> ciphertext,
        EncryptedDocumentDescriptor descriptor,
        ReadOnlySpan<byte> masterKey)
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

    private static byte[] WrapKey(byte[] dek, ReadOnlySpan<byte> masterKey)
    {
        var nonce = HashingService.GenerateNonce96();
        var wrapped = new byte[dek.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(masterKey, TagSize);
        aes.Encrypt(nonce, dek, wrapped, tag);

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
