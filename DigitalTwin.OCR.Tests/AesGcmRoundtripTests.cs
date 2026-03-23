using DigitalTwin.OCR.Services;

namespace DigitalTwin.OCR.Tests;

public class AesGcmRoundtripTests
{
    private readonly DocumentEncryptionService _sut = new();

    [Fact]
    public void Encrypt_Decrypt_Roundtrip_ProducesOriginalPlaintext()
    {
        var masterKey = HashingService.GenerateKey256();
        var plaintext = System.Text.Encoding.UTF8.GetBytes("Test medical document content — confidential.");
        var documentId = Guid.NewGuid();
        var sha256 = HashingService.ComputeSha256Hex(plaintext);

        var payload = _sut.Encrypt(plaintext, masterKey, documentId, "application/pdf", 1, sha256);
        var decrypted = _sut.Decrypt(payload.Ciphertext, payload.Descriptor, masterKey);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_SamePlaintext_ProducesDifferentCiphertext()
    {
        var masterKey = HashingService.GenerateKey256();
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };
        var sha256 = HashingService.ComputeSha256Hex(plaintext);

        var p1 = _sut.Encrypt(plaintext, masterKey, Guid.NewGuid(), "image/jpeg", 1, sha256);
        var p2 = _sut.Encrypt(plaintext, masterKey, Guid.NewGuid(), "image/jpeg", 1, sha256);

        // Different nonces must yield different ciphertexts
        Assert.NotEqual(p1.Descriptor.NonceB64, p2.Descriptor.NonceB64);
        Assert.False(p1.Ciphertext.SequenceEqual(p2.Ciphertext));
    }

    [Fact]
    public void Decrypt_WrongMasterKey_ThrowsCryptographicException()
    {
        var masterKey = HashingService.GenerateKey256();
        var wrongKey = HashingService.GenerateKey256();
        var plaintext = new byte[] { 10, 20, 30 };
        var sha256 = HashingService.ComputeSha256Hex(plaintext);

        var payload = _sut.Encrypt(plaintext, masterKey, Guid.NewGuid(), "image/jpeg", 1, sha256);

        Assert.ThrowsAny<Exception>(() =>
            _sut.Decrypt(payload.Ciphertext, payload.Descriptor, wrongKey));
    }

    [Fact]
    public void Encrypt_EmptyPlaintext_Succeeds()
    {
        var masterKey = HashingService.GenerateKey256();
        var plaintext = Array.Empty<byte>();
        var sha256 = HashingService.ComputeSha256Hex(plaintext);

        var payload = _sut.Encrypt(plaintext, masterKey, Guid.NewGuid(), "application/pdf", 0, sha256);
        var decrypted = _sut.Decrypt(payload.Ciphertext, payload.Descriptor, masterKey);

        Assert.Empty(decrypted);
    }

    // ── Hashing utilities ────────────────────────────────────────────────────

    [Fact]
    public void ComputeSha256Hex_SameInput_ProducesDeterministicHash()
    {
        var data = new byte[] { 1, 2, 3 };
        var h1 = HashingService.ComputeSha256Hex(data);
        var h2 = HashingService.ComputeSha256Hex(data);
        Assert.Equal(h1, h2);
        Assert.Equal(64, h1.Length); // 32 bytes hex = 64 chars
    }

    [Fact]
    public void GenerateKey256_ProducesUniqueKeys()
    {
        var k1 = HashingService.GenerateKey256();
        var k2 = HashingService.GenerateKey256();
        Assert.Equal(32, k1.Length);
        Assert.False(k1.SequenceEqual(k2));
    }

    [Fact]
    public void GenerateNonce96_ProducesUniqueNonces()
    {
        var n1 = HashingService.GenerateNonce96();
        var n2 = HashingService.GenerateNonce96();
        Assert.Equal(12, n1.Length);
        Assert.False(n1.SequenceEqual(n2));
    }
}
