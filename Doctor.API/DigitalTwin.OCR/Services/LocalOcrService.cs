using DigitalTwin.OCR.Models;
using DigitalTwin.OCR.Models.Enums;
using DigitalTwin.OCR.Models.Graph;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// On-device OCR using Apple's Vision framework (VNRecognizeTextRequest).
/// Fully local — no network calls, no cloud API.
/// Supports both Fast and Accurate recognition levels.
/// When buildGraph is true, also emits a per-word OcrDocumentGraph alongside the existing block output.
/// </summary>
public sealed class LocalOcrService
{
    private static readonly string[] PreferredLanguages = ["ro-RO", "en-US", "de-DE", "fr-FR"];
    private readonly ILogger<LocalOcrService> _logger;

    public LocalOcrService(ILogger<LocalOcrService> logger) => _logger = logger;

#if IOS || MACCATALYST
    private sealed record PdfTextExtraction(
        int PageCount,
        IReadOnlyList<string> PageTexts,
        float TextQualityScore);

    public async Task<OcrResult<OcrExtractionResult>> RunOcrAsync(
        string documentPath,
        DocumentMimeType mimeType,
        bool accurateMode = true,
        bool buildGraph = false,
        CancellationToken ct = default)
    {
        try
        {
            var pages = new List<OcrPage>();
            var graphPages = buildGraph ? new List<OcrGraphPage>() : null;
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
            {
                // Prefer direct text extraction for selectable-text PDFs to avoid OCR noise on tables.
                var pdfText = TryExtractPdfText(documentPath);
                if (pdfText is not null && pdfText.TextQualityScore >= 0.35f)
                {
                    _logger.LogDebug(
                        "[OCR PDF] Using direct PDF text extraction. Pages={Pages} Quality={Q:F2}",
                        pdfText.PageCount, pdfText.TextQualityScore);

                    for (var i = 0; i < pdfText.PageCount; i++)
                    {
                        var text = pdfText.PageTexts[i];
                        pages.Add(new OcrPage(
                            PageIndex: i,
                            Blocks:
                            [
                                new OcrTextBlock(
                                    Text: text,
                                    Confidence: 1.0f,
                                    Lines: [new OcrLine(text, 1.0f, 0, 0, 1, 1)])
                            ],
                            Status: OcrExecutionStatus.Success,
                            DetectedLanguage: languages.FirstOrDefault()));
                    }
                }
                else
                {
                    var (pdfPages, pdfGraphPages) = await RecognizePdfPagesAsync(
                        documentPath, languages, accurateMode, buildGraph, ct);
                    pages.AddRange(pdfPages);
                    graphPages?.AddRange(pdfGraphPages);
                }
            }
            else
            {
                var (imgPage, imgGraphPage) = await RecognizeImagePageAsync(
                    documentPath, 0, languages, accurateMode, buildGraph);
                pages.Add(imgPage);
                if (imgGraphPage is not null)
                    graphPages?.Add(imgGraphPage);
            }

            var overallStatus = pages.All(p => p.Status == OcrExecutionStatus.Success)
                ? OcrExecutionStatus.Success
                : OcrExecutionStatus.Failed;

            OcrDocumentGraph? graph = null;
            if (buildGraph && graphPages is not null)
            {
                var allTokens = graphPages.SelectMany(p => p.Tokens).ToList();
                graph = new OcrDocumentGraph(
                    graphPages,
                    allTokens,
                    languages.FirstOrDefault() ?? "en-US");
                _logger.LogDebug("[OCR Graph] Built graph with {TokenCount} tokens across {PageCount} pages.",
                    allTokens.Count, graphPages.Count);
            }

            return OcrResult<OcrExtractionResult>.Ok(new OcrExtractionResult(
                Pages: pages,
                OverallStatus: overallStatus,
                DetectedLanguage: languages.FirstOrDefault() ?? "en-US",
                IsRomanianSupported: isRomanianSupported)
            {
                Graph = graph
            });
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

    private static async Task<(List<OcrPage> pages, List<OcrGraphPage> graphPages)> RecognizePdfPagesAsync(
        string pdfPath, string[] languages, bool accurateMode, bool buildGraph, CancellationToken ct)
    {
        var url = Foundation.NSUrl.FromFilename(pdfPath);
        var pdf = new PdfKit.PdfDocument(url);
        if (pdf is null) return ([], []);

        var pages = new List<OcrPage>();
        var graphPages = new List<OcrGraphPage>();
        // 2480x3508 ≈ A4 @ 300 DPI (helps small fonts in lab PDFs).
        var thumbSize = new CoreGraphics.CGSize(2480, 3508);

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

                // Fit the PDF page into the render bitmap. Without this, some PDFs end up tiny/misaligned.
                var pageRect = page.GetBoundsForBox(PdfKit.PdfDisplayBox.Media);
                var scaleX = thumbSize.Width / pageRect.Width;
                var scaleY = thumbSize.Height / pageRect.Height;
                var scale = Math.Min(scaleX, scaleY);

                var scaledW = pageRect.Width * scale;
                var scaledH = pageRect.Height * scale;
                var offsetX = (thumbSize.Width - scaledW) / 2.0;
                var offsetY = (thumbSize.Height - scaledH) / 2.0;

                ctx.CGContext.SaveState();
                // Flip Y to match PDF coordinate system.
                ctx.CGContext.TranslateCTM(0, thumbSize.Height);
                ctx.CGContext.ScaleCTM(1, -1);
                ctx.CGContext.TranslateCTM((nfloat)offsetX, (nfloat)offsetY);
                ctx.CGContext.ScaleCTM((nfloat)scale, (nfloat)scale);
                ctx.CGContext.TranslateCTM(-pageRect.X, -pageRect.Y);
                page.Draw(PdfKit.PdfDisplayBox.Media, ctx.CGContext);
                ctx.CGContext.RestoreState();
            });

            using var cgImage = uiImage.CGImage;
            if (cgImage is null) continue;

            var (ocrPage, graphPage) = await RecognizeFromCgImageAsync(
                cgImage, i, languages, accurateMode, buildGraph);
            pages.Add(ocrPage);
            if (graphPage is not null)
                graphPages.Add(graphPage);
        }
        return (pages, graphPages);
    }

    private PdfTextExtraction? TryExtractPdfText(string pdfPath)
    {
        try
        {
            var url = Foundation.NSUrl.FromFilename(pdfPath);
            var pdf = new PdfKit.PdfDocument(url);
            if (pdf is null) return null;

            var pageCount = (int)pdf.PageCount;
            if (pageCount <= 0) return null;

            var texts = new List<string>(capacity: pageCount);
            int totalChars = 0;
            int alphaChars = 0;

            for (var i = 0; i < pageCount; i++)
            {
                var page = pdf.GetPage((nint)i);
                var pageText = page?.Text ?? string.Empty;
                texts.Add(pageText);

                totalChars += pageText.Length;
                alphaChars += pageText.Count(char.IsLetter);
            }

            // Quality heuristic: per-page text density + alphabetic ratio.
            var density = totalChars / (float)Math.Max(1, pageCount);
            var alphaRatio = alphaChars / (float)Math.Max(1, totalChars);
            var quality = Math.Clamp((density / 300f) * 0.6f + alphaRatio * 0.4f, 0f, 1f);

            return new PdfTextExtraction(pageCount, texts, quality);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[OCR PDF] PDF text extraction failed; falling back to OCR.");
            return null;
        }
    }

    private static async Task<(OcrPage page, OcrGraphPage? graphPage)> RecognizeImagePageAsync(
        string imagePath, int pageIndex, string[] languages, bool accurateMode, bool buildGraph)
    {
        var url = Foundation.NSUrl.FromFilename(imagePath);
        var source = ImageIO.CGImageSource.FromUrl(url, (ImageIO.CGImageOptions?)null);
        if (source is null)
            return (new OcrPage(pageIndex, [], OcrExecutionStatus.Failed, null), null);

        using var cgImage = source.CreateImage(0, (ImageIO.CGImageOptions?)null);
        if (cgImage is null)
            return (new OcrPage(pageIndex, [], OcrExecutionStatus.Failed, null), null);

        return await RecognizeFromCgImageAsync(cgImage, pageIndex, languages, accurateMode, buildGraph);
    }

    private static async Task<(OcrPage page, OcrGraphPage? graphPage)> RecognizeFromCgImageAsync(
        CoreGraphics.CGImage cgImage, int pageIndex, string[] languages, bool accurateMode, bool buildGraph)
    {
        var tcs = new TaskCompletionSource<(IReadOnlyList<OcrTextBlock> blocks,
            IReadOnlyList<(Vision.VNRecognizedTextObservation obs, Vision.VNRecognizedText candidate)> raw)>();

        var request = new Vision.VNRecognizeTextRequest((req, err) =>
        {
            var textReq = req as Vision.VNRecognizeTextRequest;
            if (err is not null || textReq?.Results is null)
            {
                tcs.TrySetResult(([], []));
                return;
            }

            var rawList = new List<(Vision.VNRecognizedTextObservation, Vision.VNRecognizedText)>();
            var blocks = textReq.Results
                .Select(obs =>
                {
                    var topCandidate = obs.TopCandidates(1).FirstOrDefault();
                    var text = topCandidate?.String ?? string.Empty;
                    var confidence = topCandidate?.Confidence ?? 0f;
                    var bb = obs.BoundingBox;

                    if (topCandidate is not null)
                        rawList.Add((obs, topCandidate));

                    return new OcrTextBlock(
                        Text: text,
                        Confidence: confidence,
                        Lines: [new OcrLine(text, confidence,
                            (float)bb.X, (float)bb.Y, (float)bb.Width, (float)bb.Height)]);
                })
                .ToList();

            tcs.TrySetResult((blocks, rawList));
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

        var (blocks2, rawObservations) = await tcs.Task;
        var status = performError is null ? OcrExecutionStatus.Success : OcrExecutionStatus.Failed;
        var ocrPage = new OcrPage(pageIndex, blocks2, status, languages.FirstOrDefault());

        if (!buildGraph)
            return (ocrPage, null);

        // Build per-word token graph from raw Vision observations
        var graphPage = BuildGraphPage(pageIndex, rawObservations, cgImage);
        return (ocrPage, graphPage);
    }

    /// <summary>
    /// Builds an OcrGraphPage by extracting per-word bounding boxes from VNRecognizedText.
    /// Falls back to line-level bounding box for any word that fails individual box extraction.
    /// </summary>
    private static OcrGraphPage BuildGraphPage(
        int pageIndex,
        IReadOnlyList<(Vision.VNRecognizedTextObservation obs, Vision.VNRecognizedText candidate)> rawObservations,
        CoreGraphics.CGImage cgImage)
    {
        var graphBlocks = new List<OcrGraphBlock>();
        var allTokens = new List<OcrToken>();
        int globalTokenIndex = 0;

        for (int blockIdx = 0; blockIdx < rawObservations.Count; blockIdx++)
        {
            var (obs, candidate) = rawObservations[blockIdx];
            var blockBb = new OcrBoundingBox(
                (float)obs.BoundingBox.X, (float)obs.BoundingBox.Y,
                (float)obs.BoundingBox.Width, (float)obs.BoundingBox.Height);

            var fullText = candidate.String ?? string.Empty;
            var lineTokens = new List<OcrToken>();

            // Split the line text into words and get per-word bounding boxes
            var words = fullText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int charOffset = 0;

            for (int wordIdx = 0; wordIdx < words.Length; wordIdx++)
            {
                var word = words[wordIdx];
                var wordRange = new Foundation.NSRange(charOffset, word.Length);
                bool approximate = false;
                OcrBoundingBox tokenBb;

                var wordObservation = candidate.GetBoundingBox(wordRange, out var boxError);
                if (boxError is null && wordObservation is not null)
                {
                    tokenBb = new OcrBoundingBox(
                        (float)wordObservation.BoundingBox.X,
                        (float)wordObservation.BoundingBox.Y,
                        (float)wordObservation.BoundingBox.Width,
                        (float)wordObservation.BoundingBox.Height);
                }
                else
                {
                    // Fall back to block-level bounding box — still useful for row clustering
                    tokenBb = blockBb;
                    approximate = true;
                }

                var token = new OcrToken(
                    TokenIndex: globalTokenIndex++,
                    Text: word,
                    Confidence: candidate.Confidence,
                    BoundingBox: tokenBb,
                    PageIndex: pageIndex,
                    BlockIndex: blockIdx,
                    LineIndex: 0,
                    IsBoundingBoxApproximate: approximate);

                lineTokens.Add(token);
                allTokens.Add(token);

                // Advance char offset: word length + space
                charOffset += word.Length + 1;
            }

            var graphLine = new OcrGraphLine(
                LineIndex: 0,
                Tokens: lineTokens,
                BoundingBox: blockBb,
                Text: fullText,
                AverageConfidence: candidate.Confidence);

            graphBlocks.Add(new OcrGraphBlock(blockIdx, [graphLine], blockBb));
        }

        return new OcrGraphPage(
            PageIndex: pageIndex,
            Blocks: graphBlocks,
            Lines: graphBlocks.SelectMany(b => b.Lines).ToList(),
            Tokens: allTokens,
            PageWidth: (float)cgImage.Width,
            PageHeight: (float)cgImage.Height);
    }
#else
    public Task<OcrResult<OcrExtractionResult>> RunOcrAsync(
        string documentPath,
        DocumentMimeType mimeType,
        bool accurateMode = true,
        bool buildGraph = false,
        CancellationToken ct = default)
        => Task.FromResult(OcrResult<OcrExtractionResult>.Fail("Vision OCR is only available on iOS/macCatalyst."));
#endif
}
