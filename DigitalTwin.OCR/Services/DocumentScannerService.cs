using DigitalTwin.OCR.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Presents VNDocumentCameraViewController for camera-based document scanning.
/// Returns the raw scanned images as JPEG bytes; the caller stores them in quarantine.
/// </summary>
public sealed class DocumentScannerService
{
    private readonly VaultService _vault;
    private readonly FileProtectionService _fileProtection;
    private readonly ILogger<DocumentScannerService> _logger;

    public DocumentScannerService(
        VaultService vault,
        FileProtectionService fileProtection,
        ILogger<DocumentScannerService> logger)
    {
        _vault = vault;
        _fileProtection = fileProtection;
        _logger = logger;
    }

#if IOS || MACCATALYST
    public Task<OcrResult<IReadOnlyList<string>>> ScanAsync(CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<OcrResult<IReadOnlyList<string>>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
        {
            var scanner = new VisionKit.VNDocumentCameraViewController();
            var delegate_ = new ScannerDelegate(_vault, _fileProtection, tcs, _logger);
            scanner.Delegate = delegate_;

            var vc = Platform.GetCurrentUIViewController();
            if (vc is null)
            {
                tcs.TrySetResult(OcrResult<IReadOnlyList<string>>.Fail("No UIViewController available."));
                return;
            }

            vc.PresentViewController(scanner, animated: true, completionHandler: null);
        });

        return tcs.Task;
    }

    private sealed class ScannerDelegate : VisionKit.VNDocumentCameraViewControllerDelegate
    {
        private readonly VaultService _vault;
        private readonly FileProtectionService _protection;
        private readonly TaskCompletionSource<OcrResult<IReadOnlyList<string>>> _tcs;
        private readonly ILogger _logger;

        public ScannerDelegate(
            VaultService vault,
            FileProtectionService protection,
            TaskCompletionSource<OcrResult<IReadOnlyList<string>>> tcs,
            ILogger logger)
        {
            _vault = vault;
            _protection = protection;
            _tcs = tcs;
            _logger = logger;
        }

        public override void DidFinish(
            VisionKit.VNDocumentCameraViewController controller,
            VisionKit.VNDocumentCameraScan scan)
        {
            controller.DismissViewController(animated: true, completionHandler: null);

            try
            {
                var paths = new List<string>();
                for (nuint i = 0; i < scan.PageCount; i++)
                {
                    var image = scan.GetImage(i);
                    var opaque = $"scan_{Guid.NewGuid():N}_{i}.jpg";
                    var path = _vault.QuarantinePath(opaque);

                    var data = image.AsJPEG(0.92f);
                    if (data is null) continue;

                    File.WriteAllBytes(path, data.ToArray());
                    _protection.ApplyCompleteProtection(path);
                    paths.Add(path);
                }
                _tcs.TrySetResult(OcrResult<IReadOnlyList<string>>.Ok(paths));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OCR Scanner] DidFinish failed.");
                _tcs.TrySetResult(OcrResult<IReadOnlyList<string>>.Fail("Error saving scanned pages."));
            }
        }

        public override void DidFail(
            VisionKit.VNDocumentCameraViewController controller, Foundation.NSError error)
        {
            controller.DismissViewController(animated: true, completionHandler: null);
            _tcs.TrySetResult(OcrResult<IReadOnlyList<string>>.Fail(
                $"Scanner failed: {error.LocalizedDescription}"));
        }

        public override void DidCancel(VisionKit.VNDocumentCameraViewController controller)
        {
            controller.DismissViewController(animated: true, completionHandler: null);
            _tcs.TrySetResult(OcrResult<IReadOnlyList<string>>.Fail("Scan was cancelled."));
        }
    }
#else
    public Task<OcrResult<IReadOnlyList<string>>> ScanAsync(CancellationToken ct = default)
        => Task.FromResult(OcrResult<IReadOnlyList<string>>.Fail("Document scanning is only available on iOS/macCatalyst."));
#endif
}
