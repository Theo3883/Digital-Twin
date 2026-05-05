using System.Security.Cryptography;

namespace DigitalTwin.Mobile.OCR.Services;

/// <summary>SHA-256 hashing for document integrity — BCL only, no NuGet required.</summary>
public sealed class HashingService
{
    public static string ComputeSha256Hex(ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(data, hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ComputeSha256Base64(ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(data, hash);
        return Convert.ToBase64String(hash);
    }

    public static byte[] GenerateKey256() => RandomNumberGenerator.GetBytes(32);

    public static byte[] GenerateNonce96() => RandomNumberGenerator.GetBytes(12);
}
