using DigitalTwin.OCR.Models;
using DigitalTwin.OCR.Models.Enums;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// On-device OCR using Apple's Vision framework (VNRecognizeTextRequest).
/// Fully local — no network calls, no cloud API.
/// Supports both Fast and Accurate recognition levels.
/// </summary>
public sealed class LocalOcrService
{
    private static readonly string[] PreferredLanguages = ["ro-RO", "en-US", "de-DE", "fr-FR"];
    private readonly ILogger<LocalOcrService> _logger;

    public LocalOcrService(ILogger<LocalOcrService> logger) => _logger = logger;

#if IOS || MACCATALYST
    public async Task<OcrResult<OcrExtractionResult>> RunOcrAsync(
        string documentPath,
        DocumentMimeType mimeType,
        bool accurateMode = true,
        CancellationToken ct = default)
    {
        try
        {
            var pages = new List<OcrPage>();
            var level = accurateMode
                ? Vision.VNRequestTextRecognitionLevel.Accurate
                : Vision.VNRequestTextRecognitionLevel.Fast;

            // Synchronous call; run on background thread to avoid blocking UI.
            var supported = await Task.Run(() =>
                Vision.VNRecognizeTextRequest.GetSupportedRecognitionLanguages(
                    level, Vision.VNRecognizeTextRequestRevision.Unspecified, out _) ?? []);

            var supportedSet = new HashSet<string>(supported);
            var languages = PreferredLanguages.Where(l => supportedSet.Contains(l)).ToArray();
            var isRomanianSupported = supportedSet.Contains("ro-RO");

            if (mimeType == DocumentMimeType.Pdf)
                pages.AddRange(await RecognizePdfPagesAsync(documentPath, languages, accurateMode, ct));
            else
                pages.Add(await RecognizeImagePageAsync(documentPath, 0, languages, accurateMode));

            var overallStatus = pages.All(p => p.Status == OcrExecutionStatus.Success)
                ? OcrExecutionStatus.Success
                : OcrExecutionStatus.Failed;

            return OcrResult<OcrExtractionResult>.Ok(new OcrExtractionResult(
                Pages: pages,
                OverallStatus: overallStatus,
                DetectedLanguage: languages.FirstOrDefault() ?? "en-US",
                IsRomanianSupported: isRomanianSupported));
        }
        catch (OperationCanceledException)
        {
            return OcrResult<OcrExtractionResult>.Fail("OCR was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR] RunOcrAsync exception.");
            return OcrResult<OcrExtractionResult>.Fail("OCR failed — see logs.");
        }
    }

    private static async Task<IEnumerable<OcrPage>> RecognizePdfPagesAsync(
        string pdfPath, string[] languages, bool accurateMode, CancellationToken ct)
    {
        var url = Foundation.NSUrl.FromFilename(pdfPath);
        var pdf = new PdfKit.PdfDocument(url);
        if (pdf is null) return [];

        var pages = new List<OcrPage>();
        var thumbSize = new CoreGraphics.CGSize(1653, 2339); // A4 @ 200 DPI

        for (var i = 0; i < (int)pdf.PageCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            var page = pdf.GetPage((nint)i);
            if (page is null) continue;

            // Render PDF page to UIImage for Vision
            using var bitmap = new UIKit.UIGraphicsImageRenderer(thumbSize);
            var uiImage = bitmap.CreateImage(ctx =>
            {
                ctx.CGContext.SetFillColor(UIKit.UIColor.White.CGColor);
                ctx.CGContext.FillRect(new CoreGraphics.CGRect(CoreGraphics.CGPoint.Empty, thumbSize));
                page.Draw(PdfKit.PdfDisplayBox.Media, ctx.CGContext);
            });

            using var cgImage = uiImage.CGImage;
            if (cgImage is null) continue;

            pages.Add(await RecognizeFromCgImageAsync(cgImage, i, languages, accurateMode));
        }
        return pages;
    }

    private static async Task<OcrPage> RecognizeImagePageAsync(
        string imagePath, int pageIndex, string[] languages, bool accurateMode)
    {
        var url = Foundation.NSUrl.FromFilename(imagePath);
        var source = ImageIO.CGImageSource.FromUrl(url, (ImageIO.CGImageOptions?)null);
        if (source is null)
            return new OcrPage(pageIndex, [], OcrExecutionStatus.Failed, null);

        using var cgImage = source.CreateImage(0, (ImageIO.CGImageOptions?)null);
        if (cgImage is null)
            return new OcrPage(pageIndex, [], OcrExecutionStatus.Failed, null);

        return await RecognizeFromCgImageAsync(cgImage, pageIndex, languages, accurateMode);
    }

    private static async Task<OcrPage> RecognizeFromCgImageAsync(
        CoreGraphics.CGImage cgImage, int pageIndex, string[] languages, bool accurateMode)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<OcrTextBlock>>();

        var request = new Vision.VNRecognizeTextRequest((req, err) =>
        {
            var textReq = req as Vision.VNRecognizeTextRequest;
            if (err is not null || textReq?.Results is null)
            {
                tcs.TrySetResult([]);
                return;
            }

            var blocks = textReq.Results
                .Select(obs =>
                {
                    var topCandidate = obs.TopCandidates(1).FirstOrDefault();
                    var text = topCandidate?.String ?? string.Empty;
                    var confidence = topCandidate?.Confidence ?? 0f;
                    var bb = obs.BoundingBox;

                    return new OcrTextBlock(
                        Text: text,
                        Confidence: confidence,
                        Lines: [new OcrLine(text, confidence, (float)bb.X, (float)bb.Y, (float)bb.Width, (float)bb.Height)]);
                })
                .ToList();

            tcs.TrySetResult(blocks);
        })
        {
            RecognitionLevel = accurateMode
                ? Vision.VNRequestTextRecognitionLevel.Accurate
                : Vision.VNRequestTextRecognitionLevel.Fast,
            RecognitionLanguages = languages,
            UsesLanguageCorrection = true
        };

        var handler = new Vision.VNImageRequestHandler(cgImage, new Foundation.NSDictionary());
        handler.Perform([request], out Foundation.NSError? performError);

        var blocks2 = await tcs.Task;
        var status = performError is null ? OcrExecutionStatus.Success : OcrExecutionStatus.Failed;

        return new OcrPage(pageIndex, blocks2, status, languages.FirstOrDefault());
    }
#else
    public Task<OcrResult<OcrExtractionResult>> RunOcrAsync(
        string documentPath,
        DocumentMimeType mimeType,
        bool accurateMode = true,
        CancellationToken ct = default)
        => Task.FromResult(OcrResult<OcrExtractionResult>.Fail("Vision OCR is only available on iOS/macCatalyst."));
#endif
}
