using System.Security.Cryptography;

namespace DigitalTwin.OCR.Services;

/// <summary>SHA-256 hashing for document integrity — BCL only, no NuGet required.</summary>
public sealed class HashingService
{
    /// <summary>Returns the hex-encoded SHA-256 of the given bytes.</summary>
    public static string ComputeSha256Hex(ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(data, hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Returns the base64-encoded SHA-256 of the given bytes.</summary>
    public static string ComputeSha256Base64(ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(data, hash);
        return Convert.ToBase64String(hash);
    }

    /// <summary>Computes a random 256-bit (32-byte) key using a CSPRNG.</summary>
    public static byte[] GenerateKey256() => RandomNumberGenerator.GetBytes(32);

    /// <summary>Computes a random 96-bit (12-byte) AES-GCM nonce.</summary>
    public static byte[] GenerateNonce96() => RandomNumberGenerator.GetBytes(12);
}
