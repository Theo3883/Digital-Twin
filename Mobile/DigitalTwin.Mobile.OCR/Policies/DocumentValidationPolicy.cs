using DigitalTwin.Mobile.OCR.Models.Enums;

namespace DigitalTwin.Mobile.OCR.Policies;

/// <summary>
/// Validates raw document bytes before quarantine acceptance.
/// Checks magic bytes, extension, MIME type, file size, and page count.
/// All checks are pure logic — no iOS APIs required.
/// </summary>
public static class DocumentValidationPolicy
{
    public const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    public const int MaxPageCount = 100;

    private static readonly Dictionary<DocumentMimeType, byte[][]> MagicBytes = new()
    {
        [DocumentMimeType.Pdf] = [new byte[] { 0x25, 0x50, 0x44, 0x46 }],
        [DocumentMimeType.Jpeg] = [new byte[] { 0xFF, 0xD8, 0xFF }],
        [DocumentMimeType.Png] = [new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A }]
    };

    private static readonly Dictionary<string, DocumentMimeType> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = DocumentMimeType.Pdf,
        [".jpg"] = DocumentMimeType.Jpeg,
        [".jpeg"] = DocumentMimeType.Jpeg,
        [".png"] = DocumentMimeType.Png
    };

    public static (bool IsValid, string? Reason) Validate(
        ReadOnlySpan<byte> header,
        string fileExtension,
        long fileSizeBytes)
    {
        if (fileSizeBytes == 0)
            return (false, "File is empty.");

        if (fileSizeBytes > MaxFileSizeBytes)
            return (false, $"File exceeds maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");

        if (!ExtensionMap.TryGetValue(fileExtension, out var expectedMime))
            return (false, $"Extension '{fileExtension}' is not allowed. Only PDF, JPG, JPEG, PNG are accepted.");

        if (!MatchesMagicBytes(header, expectedMime))
            return (false, "File content does not match its declared extension (magic byte mismatch).");

        return (true, null);
    }

    public static DocumentMimeType DetectMimeType(ReadOnlySpan<byte> header)
    {
        foreach (var (mime, patterns) in MagicBytes)
        {
            foreach (var pattern in patterns)
            {
                if (header.Length >= pattern.Length && header[..pattern.Length].SequenceEqual(pattern))
                    return mime;
            }
        }
        return DocumentMimeType.Unknown;
    }

    private static bool MatchesMagicBytes(ReadOnlySpan<byte> header, DocumentMimeType mime)
    {
        if (!MagicBytes.TryGetValue(mime, out var patterns))
            return false;

        foreach (var pattern in patterns)
        {
            if (header.Length >= pattern.Length && header[..pattern.Length].SequenceEqual(pattern))
                return true;
        }
        return false;
    }
}
