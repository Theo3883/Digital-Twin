using DigitalTwin.OCR.Models;
using DigitalTwin.OCR.Services;
using Microsoft.Extensions.Logging;
using UIKit;
using Foundation;
using PdfKit;

namespace DigitalTwin.Services;

public sealed class DocumentPreviewService(
    VaultService vault,
    ILogger<DocumentPreviewService> logger)
    : IDocumentPreviewService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _observerGate = new();
    private NSObject? _bgObserver;

    public async Task<OcrResult<bool>> PreviewAsync(
        Guid documentId,
        string mimeType,
        byte[]? plaintextBytes,
        CancellationToken ct = default)
    {
        if (plaintextBytes is null || plaintextBytes.Length == 0)
            return OcrResult<bool>.Fail("Document content is empty.");

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var dismissalTcs = new TaskCompletionSource<OcrResult<bool>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            SubscribeToBackgroundLock();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    var vc = Platform.GetCurrentUIViewController();
                    if (vc is null)
                    {
                        dismissalTcs.TrySetResult(OcrResult<bool>.Fail("No UIViewController available for preview."));
                        return;
                    }

                    var host = new DocumentPreviewHostController(
                        documentId: documentId,
                        mimeType: mimeType,
                        plaintextBytes: plaintextBytes,
                        dismissalTcs: dismissalTcs);

                    host.ModalPresentationStyle = UIModalPresentationStyle.FullScreen;
                    vc.PresentViewController(host, animated: true, completionHandler: null);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[DocPreview] Failed to present preview host.");
                    dismissalTcs.TrySetResult(OcrResult<bool>.Fail("Could not present document preview."));
                }
            });

            // Wait until the user dismisses the preview UI.
            return await dismissalTcs.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return OcrResult<bool>.Fail("Preview was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DocPreview] PreviewAsync failed.");
            return OcrResult<bool>.Fail("Could not preview document.");
        }
        finally
        {
            UnsubscribeFromBackgroundLock();

            // Best-effort in-memory zeroization.
            try
            {
                Array.Clear(plaintextBytes, 0, plaintextBytes.Length);
            }
            catch
            {
                // ignore
            }

            // Ensure the vault is not left unlocked after preview lifecycle ends.
            try { vault.Lock(); } catch { /* ignore */ }

            _gate.Release();
        }
    }

    private void SubscribeToBackgroundLock()
    {
        lock (_observerGate)
        {
            if (_bgObserver is not null)
                return;

            _bgObserver = NSNotificationCenter.DefaultCenter.AddObserver(
                UIApplication.DidEnterBackgroundNotification,
                _ =>
                {
                    try
                    {
                        vault.Lock();
                    }
                    catch
                    {
                        // ignore
                    }
                });
        }
    }

    private void UnsubscribeFromBackgroundLock()
    {
        lock (_observerGate)
        {
            try
            {
                if (_bgObserver is not null)
                    NSNotificationCenter.DefaultCenter.RemoveObserver(_bgObserver);
            }
            catch
            {
                // ignore
            }
            finally
            {
                _bgObserver = null;
            }
        }
    }

    private sealed class DocumentPreviewHostController(
        Guid documentId,
        string mimeType,
        byte[] plaintextBytes,
        TaskCompletionSource<OcrResult<bool>> dismissalTcs)
        : UIViewController
    {
        private readonly Guid _documentId = documentId;

        private string? _previewError;
        private PdfView? _pdfView;
        private UIImageView? _imageView;
        private UIButton? _doneButton;

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            View!.BackgroundColor = UIColor.Black;

            // Done button: closes the preview UI and unblocks the waiting caller.
            _doneButton = new UIButton(UIButtonType.System);
            _doneButton.SetTitle("Done", UIControlState.Normal);
            _doneButton.SetTitleColor(UIColor.White, UIControlState.Normal);
            _doneButton.TouchUpInside += (_, _) => DismissViewController(animated: true, completionHandler: null);

            _doneButton.TranslatesAutoresizingMaskIntoConstraints = false;
            View.AddSubview(_doneButton);
            NSLayoutConstraint.ActivateConstraints([
                _doneButton.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor, 8),
                _doneButton.RightAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.RightAnchor, -12),
                _doneButton.WidthAnchor.ConstraintEqualTo(72),
                _doneButton.HeightAnchor.ConstraintEqualTo(34)
            ]);

            try
            {
                var isPdf = mimeType.Contains("pdf", StringComparison.OrdinalIgnoreCase);
                var isImage = mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

                if (isPdf)
                {
                    PresentPdf();
                }
                else if (isImage)
                {
                    PresentImage();
                }
                else
                {
                    _previewError = "Unsupported document type for in-app preview.";
                    PresentErrorLabel(_previewError);
                }
            }
            catch (Exception ex)
            {
                _previewError = "Could not render preview.";
                PresentErrorLabel(_previewError);
                Console.WriteLine($"[DocPreview] Render error: {ex}");
            }

            // Ensure the Done button is always tappable (image scroll view can intercept touches).
            if (_doneButton is not null)
                View.BringSubviewToFront(_doneButton);
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);

            // Release references to reduce the chance of keeping decrypted bytes alive.
            try
            {
                if (_pdfView is not null) _pdfView.Document = null;
                if (_imageView is not null) _imageView.Image = null;
            }
            catch
            {
                // ignore
            }

            dismissalTcs.TrySetResult(
                _previewError is null
                    ? OcrResult<bool>.Ok(true)
                    : OcrResult<bool>.Fail(_previewError));
        }

        private void PresentErrorLabel(string error)
        {
            var label = new UILabel
            {
                Text = error,
                TextColor = UIColor.White,
                Lines = 0,
                TextAlignment = UITextAlignment.Center,
                Font = UIFont.SystemFontOfSize(15)
            };

            label.TranslatesAutoresizingMaskIntoConstraints = false;
            View!.AddSubview(label);
            NSLayoutConstraint.ActivateConstraints([
                label.LeftAnchor.ConstraintEqualTo(View!.LeftAnchor, 16),
                label.RightAnchor.ConstraintEqualTo(View!.RightAnchor, -16),
                label.TopAnchor.ConstraintEqualTo(View!.SafeAreaLayoutGuide.TopAnchor, 56)
            ]);
        }

        private void PresentPdf()
        {
            var data = NSData.FromArray(plaintextBytes);
            var pdfDoc = new PdfDocument(data);

            _pdfView = new PdfView
            {
                AutoScales = true,
                Document = pdfDoc,
                TranslatesAutoresizingMaskIntoConstraints = false
            };

            View!.AddSubview(_pdfView);
            NSLayoutConstraint.ActivateConstraints([
                _pdfView.LeftAnchor.ConstraintEqualTo(View!.LeftAnchor),
                _pdfView.RightAnchor.ConstraintEqualTo(View!.RightAnchor),
                // Start below Done so the button doesn't get covered.
                _pdfView.TopAnchor.ConstraintEqualTo(_doneButton!.BottomAnchor, 8),
                _pdfView.BottomAnchor.ConstraintEqualTo(View!.BottomAnchor)
            ]);
        }

        private void PresentImage()
        {
            var data = NSData.FromArray(plaintextBytes);
            var image = UIImage.LoadFromData(data);
            if (image is null)
                throw new InvalidOperationException("UIImage could not be loaded from bytes.");

            // Zoomable container (basic): scroll view with min/max zoom.
            var scrollView = new UIScrollView
            {
                MinimumZoomScale = 1,
                MaximumZoomScale = 3,
                ZoomScale = 1,
                TranslatesAutoresizingMaskIntoConstraints = false
            };

            _imageView = new UIImageView(image)
            {
                ContentMode = UIViewContentMode.ScaleAspectFit,
                TranslatesAutoresizingMaskIntoConstraints = false
            };

            scrollView.AddSubview(_imageView);
            scrollView.Delegate = new ZoomDelegate(_imageView);

            View!.AddSubview(scrollView);

            NSLayoutConstraint.ActivateConstraints([
                scrollView.LeftAnchor.ConstraintEqualTo(View!.LeftAnchor),
                scrollView.RightAnchor.ConstraintEqualTo(View!.RightAnchor),
                // Start below Done so the button doesn't get covered.
                scrollView.TopAnchor.ConstraintEqualTo(_doneButton!.BottomAnchor, 8),
                scrollView.BottomAnchor.ConstraintEqualTo(View!.BottomAnchor),

                _imageView.WidthAnchor.ConstraintEqualTo(scrollView.WidthAnchor),
                _imageView.HeightAnchor.ConstraintEqualTo(scrollView.HeightAnchor)
            ]);
        }

        private sealed class ZoomDelegate(UIView view) : UIScrollViewDelegate
        {
            public override UIView ViewForZoomingInScrollView(UIScrollView scrollView) => view;
        }
    }
}

