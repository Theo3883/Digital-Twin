using DigitalTwin.OCR.Models;
using DigitalTwin.OCR.Models.Enums;
using DigitalTwin.OCR.Policies;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Presents UIDocumentPickerViewController for file import.
/// Validates magic bytes before accepting the file into quarantine.
/// </summary>
public sealed class FileImportService
{
    private readonly VaultService _vault;
    private readonly FileProtectionService _fileProtection;
    private readonly ILogger<FileImportService> _logger;

    public FileImportService(
        VaultService vault,
        FileProtectionService fileProtection,
        ILogger<FileImportService> logger)
    {
        _vault = vault;
        _fileProtection = fileProtection;
        _logger = logger;
    }

#if IOS || MACCATALYST
    public Task<OcrResult<(string QuarantinePath, DocumentMimeType MimeType)>> PickAndImportAsync(CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<OcrResult<(string, DocumentMimeType)>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
        {
            var contentTypes = new[]
            {
                UniformTypeIdentifiers.UTTypes.Pdf,
                UniformTypeIdentifiers.UTTypes.Jpeg,
                UniformTypeIdentifiers.UTTypes.Png
            };
            var picker = new UIKit.UIDocumentPickerViewController(contentTypes);
            picker.AllowsMultipleSelection = false;
            var delegate_ = new PickerDelegate(_vault, _fileProtection, tcs, _logger);
            picker.Delegate = delegate_;

            var vc = Platform.GetCurrentUIViewController();
            if (vc is null)
            {
                tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Fail("No UIViewController available."));
                return;
            }

            vc.PresentViewController(picker, animated: true, completionHandler: null);
        });

        return tcs.Task;
    }

    // Implement via [Export] to be compatible with both iOS and macCatalyst delegate protocols.
    private sealed class PickerDelegate : Foundation.NSObject, UIKit.IUIDocumentPickerDelegate
    {
        private readonly VaultService _vault;
        private readonly FileProtectionService _protection;
        private readonly TaskCompletionSource<OcrResult<(string, DocumentMimeType)>> _tcs;
        private readonly ILogger _logger;

        public PickerDelegate(
            VaultService vault,
            FileProtectionService protection,
            TaskCompletionSource<OcrResult<(string, DocumentMimeType)>> tcs,
            ILogger logger)
        {
            _vault = vault;
            _protection = protection;
            _tcs = tcs;
            _logger = logger;
        }

        [Foundation.Export("documentPicker:didPickDocumentsAtURLs:")]
        public void DidPickDocumentsAtUrls(UIKit.UIDocumentPickerViewController controller, Foundation.NSUrl[] urls)
        {
            controller.DismissViewController(animated: true, completionHandler: null);

            var url = urls.FirstOrDefault();
            if (url is null)
            {
                _tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Fail("No file was selected."));
                return;
            }

            var secureAccess = url.StartAccessingSecurityScopedResource();
            try
            {
                var localPath = url.Path!;
                var ext = Path.GetExtension(localPath);
                var sizeBytes = new FileInfo(localPath).Length;

                using var fs = File.OpenRead(localPath);
                var header = new byte[8];
                fs.Read(header, 0, header.Length);
                fs.Seek(0, SeekOrigin.Begin);

                var (isValid, reason) = DocumentValidationPolicy.Validate(header, ext, sizeBytes);
                if (!isValid)
                {
                    _tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Fail(reason!));
                    return;
                }

                var mime = DocumentValidationPolicy.DetectMimeType(header);
                var opaque = $"import_{Guid.NewGuid():N}{ext}";
                var destPath = _vault.QuarantinePath(opaque);

                var allBytes = new byte[sizeBytes];
                fs.Read(allBytes, 0, (int)sizeBytes);
                File.WriteAllBytes(destPath, allBytes);
                _protection.ApplyCompleteProtection(destPath);

                _tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Ok((destPath, mime)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OCR FileImport] Import failed.");
                _tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Fail("File import failed."));
            }
            finally
            {
                if (secureAccess) url.StopAccessingSecurityScopedResource();
            }
        }

        [Foundation.Export("documentPickerWasCancelled:")]
        public void WasCancelled(UIKit.UIDocumentPickerViewController controller)
        {
            controller.DismissViewController(animated: true, completionHandler: null);
            _tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Fail("File selection was cancelled."));
        }
    }
#else
    public Task<OcrResult<(string QuarantinePath, DocumentMimeType MimeType)>> PickAndImportAsync(CancellationToken ct = default)
        => Task.FromResult(OcrResult<(string, DocumentMimeType)>.Fail("File import is only available on iOS/macCatalyst."));
#endif
}
