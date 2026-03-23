using DigitalTwin.OCR.Models;
using DigitalTwin.OCR.Models.Enums;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Normalizes raw document bytes into a canonical representation before encryption.
/// PDF: re-renders each page via PdfKit to strip hidden metadata.
/// Image: corrects EXIF orientation and converts to JPEG.
/// </summary>
public sealed class DocumentNormalizationService
{
    private readonly FileProtectionService _fileProtection;
    private readonly ILogger<DocumentNormalizationService> _logger;

    public DocumentNormalizationService(
        FileProtectionService fileProtection,
        ILogger<DocumentNormalizationService> logger)
    {
        _fileProtection = fileProtection;
        _logger = logger;
    }

#if IOS || MACCATALYST
    public async Task<OcrResult<(byte[] Normalized, int PageCount, string MimeType)>> NormalizeAsync(
        string quarantinePath, DocumentMimeType mimeType)
    {
        try
        {
            var tempPath = Path.GetTempFileName();
            _fileProtection.ApplyCompleteProtection(tempPath);

            try
            {
                return mimeType switch
                {
                    DocumentMimeType.Pdf => await NormalizePdfAsync(quarantinePath, tempPath),
                    DocumentMimeType.Jpeg or DocumentMimeType.Png => await NormalizeImageAsync(quarantinePath, tempPath, mimeType),
                    _ => OcrResult<(byte[], int, string)>.Fail($"Unsupported MIME type: {mimeType}")
                };
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR Normalize] Exception during normalization.");
            return OcrResult<(byte[], int, string)>.Fail("Normalization failed.");
        }
    }

    private static Task<OcrResult<(byte[] Normalized, int PageCount, string MimeType)>> NormalizePdfAsync(
        string sourcePath, string tempPath)
    {
        var url = Foundation.NSUrl.FromFilename(sourcePath);
        var pdf = new PdfKit.PdfDocument(url);
        if (pdf is null)
            return Task.FromResult(OcrResult<(byte[], int, string)>.Fail("Could not open PDF document."));

        var pageCount = (int)pdf.PageCount;

        // Re-render each page to strip hidden metadata
        var outputPdf = new PdfKit.PdfDocument();
        for (var i = 0; i < pageCount; i++)
        {
            var page = pdf.GetPage((nint)i);
            if (page is not null)
                outputPdf.InsertPage(page, (nint)i);
        }

        var data = outputPdf.GetDataRepresentation();
        if (data is null)
            return Task.FromResult(OcrResult<(byte[], int, string)>.Fail("Could not serialise normalised PDF."));

        return Task.FromResult(OcrResult<(byte[], int, string)>.Ok((data.ToArray(), pageCount, "application/pdf")));
    }

    private static Task<OcrResult<(byte[] Normalized, int PageCount, string MimeType)>> NormalizeImageAsync(
        string sourcePath, string tempPath, DocumentMimeType mimeType)
    {
        var url = Foundation.NSUrl.FromFilename(sourcePath);
        var source = ImageIO.CGImageSource.FromUrl(url, (ImageIO.CGImageOptions?)null);
        if (source is null)
            return Task.FromResult(OcrResult<(byte[], int, string)>.Fail("Could not open image."));

        using var cgImage = source.CreateImage(0, (ImageIO.CGImageOptions?)null);
        if (cgImage is null)
            return Task.FromResult(OcrResult<(byte[], int, string)>.Fail("Could not decode image."));

        // Create UIImage from CGImage; UIKit handles EXIF orientation automatically.
        using var uiImage = new UIKit.UIImage(cgImage);
        var jpegData = uiImage.AsJPEG(0.9f);
        if (jpegData is null)
            return Task.FromResult(OcrResult<(byte[], int, string)>.Fail("JPEG re-encoding failed."));

        return Task.FromResult(OcrResult<(byte[], int, string)>.Ok((jpegData.ToArray(), 1, "image/jpeg")));
    }
#else
    public Task<OcrResult<(byte[] Normalized, int PageCount, string MimeType)>> NormalizeAsync(
        string quarantinePath, DocumentMimeType mimeType)
        => Task.FromResult(OcrResult<(byte[], int, string)>.Fail("Normalization is only available on iOS/macCatalyst."));
#endif
}
