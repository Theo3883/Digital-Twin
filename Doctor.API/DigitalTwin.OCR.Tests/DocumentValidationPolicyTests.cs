using DigitalTwin.OCR.Models.Enums;
using DigitalTwin.OCR.Policies;

namespace DigitalTwin.OCR.Tests;

public class DocumentValidationPolicyTests
{
    // ── Magic bytes ──────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidPdfBytes_ReturnsSuccess()
    {
        var header = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 }; // %PDF-1.7
        var (isValid, reason) = DocumentValidationPolicy.Validate(header, ".pdf", 1024);
        Assert.True(isValid);
        Assert.Null(reason);
    }

    [Fact]
    public void Validate_ValidJpegBytes_ReturnsSuccess()
    {
        var header = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 }; // JFIF
        var (isValid, _) = DocumentValidationPolicy.Validate(header, ".jpg", 512 * 1024);
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_ValidPngBytes_ReturnsSuccess()
    {
        var header = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // ‰PNG
        var (isValid, _) = DocumentValidationPolicy.Validate(header, ".png", 256 * 1024);
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_MismatchedExtensionAndMagicBytes_ReturnsFail()
    {
        var pdfHeader = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 };
        var (isValid, reason) = DocumentValidationPolicy.Validate(pdfHeader, ".jpg", 1024);
        Assert.False(isValid);
        Assert.NotNull(reason);
        Assert.Contains("magic byte", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_EmptyFile_ReturnsFail()
    {
        var (isValid, reason) = DocumentValidationPolicy.Validate(Array.Empty<byte>(), ".pdf", 0);
        Assert.False(isValid);
        Assert.Contains("empty", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_FileTooLarge_ReturnsFail()
    {
        var header = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 };
        var (isValid, reason) = DocumentValidationPolicy.Validate(header, ".pdf", 51 * 1024 * 1024);
        Assert.False(isValid);
        Assert.Contains("50 MB", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_DisallowedExtension_ReturnsFail()
    {
        var header = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var (isValid, reason) = DocumentValidationPolicy.Validate(header, ".exe", 1024);
        Assert.False(isValid);
        Assert.Contains("not allowed", reason!, StringComparison.OrdinalIgnoreCase);
    }

    // ── MIME detection ───────────────────────────────────────────────────────

    [Fact]
    public void DetectMimeType_PdfHeader_ReturnsPdf()
    {
        var header = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };
        Assert.Equal(DocumentMimeType.Pdf, DocumentValidationPolicy.DetectMimeType(header));
    }

    [Fact]
    public void DetectMimeType_JpegHeader_ReturnsJpeg()
    {
        var header = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        Assert.Equal(DocumentMimeType.Jpeg, DocumentValidationPolicy.DetectMimeType(header));
    }

    [Fact]
    public void DetectMimeType_UnknownHeader_ReturnsUnknown()
    {
        var header = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        Assert.Equal(DocumentMimeType.Unknown, DocumentValidationPolicy.DetectMimeType(header));
    }
}
