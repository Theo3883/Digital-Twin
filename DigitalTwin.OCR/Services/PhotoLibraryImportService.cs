using System.Diagnostics;
using DigitalTwin.OCR.Models;
using DigitalTwin.OCR.Models.Enums;
using DigitalTwin.OCR.Policies;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Presents the system photo picker (PHPicker) so users can choose images from the gallery
/// without the Files / iCloud document UI. Images are re-encoded as JPEG for the OCR pipeline.
/// </summary>
public sealed class PhotoLibraryImportService
{
    private const string LogPrefix = "[OCR PhotoLibrary]";

    private readonly VaultService _vault;
    private readonly FileProtectionService _fileProtection;
    private readonly ILogger<PhotoLibraryImportService> _logger;

    public PhotoLibraryImportService(
        VaultService vault,
        FileProtectionService fileProtection,
        ILogger<PhotoLibraryImportService> logger)
    {
        _vault = vault;
        _fileProtection = fileProtection;
        _logger = logger;
    }

#if IOS || MACCATALYST
    private static readonly ObjCRuntime.Class s_uiImageClass = new(typeof(UIKit.UIImage));

    public Task<OcrResult<(string QuarantinePath, DocumentMimeType MimeType)>> PickAndImportAsync(CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<OcrResult<(string, DocumentMimeType)>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        ct.Register(() => tcs.TrySetCanceled());

        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                Diag(_logger, "Presenting PHPicker (full screen).");

                var config = new PhotosUI.PHPickerConfiguration
                {
                    SelectionLimit = 1,
                    Filter = PhotosUI.PHPickerFilter.ImagesFilter
                };

                var picker = new PhotosUI.PHPickerViewController(config);
                var del = new PhotoPickerDelegate(_vault, _fileProtection, tcs, _logger);
                picker.Delegate = del;
                picker.ModalPresentationStyle = UIKit.UIModalPresentationStyle.FullScreen;

                var vc = Platform.GetCurrentUIViewController();
                if (vc is null)
                {
                    Diag(_logger, "ERROR: Platform.GetCurrentUIViewController() returned null.");
                    tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Fail("No UIViewController available."));
                    return;
                }

                vc.PresentViewController(picker, animated: true, completionHandler: null);
                Diag(_logger, "PHPicker PresentViewController invoked.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Failed to present picker.", LogPrefix);
                Diag(_logger, $"EXCEPTION present picker: {ex}");
                tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Fail("Could not open photo library."));
            }
        });

        return tcs.Task;
    }

    /// <summary>
    /// Logs to both <see cref="ILogger"/> (visible when DigitalTwin.OCR filter is Debug) and
    /// <see cref="Debug"/> output (always visible in the IDE Debug / Output window for Debug builds).
    /// </summary>
    internal static void Diag(ILogger logger, string message)
    {
        logger.LogDebug("{Prefix} {Message}", LogPrefix, message);
        Debug.WriteLine($"{LogPrefix} {message}");
    }

    private sealed class PhotoPickerDelegate : Foundation.NSObject, PhotosUI.IPHPickerViewControllerDelegate
    {
        private readonly VaultService _vault;
        private readonly FileProtectionService _protection;
        private readonly TaskCompletionSource<OcrResult<(string, DocumentMimeType)>> _tcs;
        private readonly ILogger _logger;

        public PhotoPickerDelegate(
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

        [Foundation.Export("picker:didFinishPicking:")]
        public void DidFinishPicking(PhotosUI.PHPickerViewController picker, PhotosUI.PHPickerResult[] results)
        {
            picker.DismissViewController(animated: true, completionHandler: null);

            var count = results?.Length ?? 0;
            Diag(_logger, $"DidFinishPicking: resultCount={count}");

            var result = results?.FirstOrDefault();
            if (result is null || result.ItemProvider is null)
            {
                Diag(_logger, "User cancelled or no ItemProvider.");
                _tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Fail("No photo was selected."));
                return;
            }

            var provider = result.ItemProvider;
            LogRegisteredTypes(_logger, provider);

            // Try multiple load strategies — PHPicker sometimes delivers types that don't load as UIImage via LoadObject.
            TryImportFromItemProvider(provider);
        }

        private void LogRegisteredTypes(ILogger logger, Foundation.NSItemProvider provider)
        {
            try
            {
                var ids = provider.RegisteredTypeIdentifiers;
                if (ids is null || ids.Length == 0)
                {
                    Diag(logger, "RegisteredTypeIdentifiers: (empty)");
                    return;
                }

                var list = string.Join(", ", Enumerable.Range(0, (int)ids.Length).Select(i => ids[i]?.ToString() ?? "?"));
                Diag(logger, $"RegisteredTypeIdentifiers ({ids.Length}): {list}");
            }
            catch (Exception ex)
            {
                Diag(logger, $"RegisteredTypeIdentifiers: could not read — {ex.Message}");
            }
        }

        private void TryImportFromItemProvider(Foundation.NSItemProvider provider)
        {
            var canUiImage = provider.CanLoadObject(s_uiImageClass);
            Diag(_logger, $"CanLoadObject(UIImage)={canUiImage}");

            // 1) Prefer file representation (often most reliable for PHPicker).
            TryLoadFileRepresentations(provider, 0);
        }

        private void TryLoadFileRepresentations(Foundation.NSItemProvider provider, int index)
        {
            var types = new[]
            {
                UniformTypeIdentifiers.UTTypes.Jpeg.Identifier,
                UniformTypeIdentifiers.UTTypes.Png.Identifier,
                UniformTypeIdentifiers.UTTypes.Heic.Identifier,
                UniformTypeIdentifiers.UTTypes.Tiff.Identifier,
                "public.image"
            };

            if (index >= types.Length)
            {
                Diag(_logger, "File representation chain exhausted — trying LoadObject(UIImage).");
                TryLoadUiImageObject(provider);
                return;
            }

            var uti = types[index];
            Diag(_logger, $"LoadFileRepresentation attempt [{index}] UTI={uti}");

            provider.LoadFileRepresentation(uti, (url, err) =>
            {
                if (err is not null)
                {
                    Diag(_logger, $"LoadFileRepresentation UTI={uti} error: {err.LocalizedDescription} (code {err.Code})");
                    TryLoadFileRepresentations(provider, index + 1);
                    return;
                }

                if (url is null || string.IsNullOrEmpty(url.Path))
                {
                    Diag(_logger, $"LoadFileRepresentation UTI={uti} returned null/empty URL.");
                    TryLoadFileRepresentations(provider, index + 1);
                    return;
                }

                try
                {
                    var path = url.Path!;
                    var len = new FileInfo(path).Length;
                    Diag(_logger, $"LoadFileRepresentation success: path len={len} bytes");

                    var bytes = File.ReadAllBytes(path);
                    FinalizeBytes(bytes, $"file:{uti}", mainThreadDecode: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "{Prefix} Reading temp file failed for UTI {Uti}", LogPrefix, uti);
                    Diag(_logger, $"Reading file failed: {ex.Message}");
                    TryLoadFileRepresentations(provider, index + 1);
                }
            });
        }

        private void TryLoadUiImageObject(Foundation.NSItemProvider provider)
        {
            if (!provider.CanLoadObject(s_uiImageClass))
            {
                Diag(_logger, "CanLoadObject(UIImage) is false — trying LoadDataRepresentation chain.");
                TryLoadDataRepresentations(provider, 0);
                return;
            }

            Diag(_logger, "Calling LoadObject(UIImage)…");
            provider.LoadObject(s_uiImageClass, (obj, err) =>
            {
                Diag(_logger, $"LoadObject callback: err={(err is null ? "null" : err.LocalizedDescription)}, objType={obj?.GetType().FullName ?? "null"}");

                void Run()
                {
                    try
                    {
                        if (err is not null)
                        {
                            _tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Fail(err.LocalizedDescription));
                            return;
                        }

                        if (obj is null)
                        {
                            Diag(_logger, "LoadObject returned null object — trying LoadDataRepresentation.");
                            TryLoadDataRepresentations(provider, 0);
                            return;
                        }

                        UIKit.UIImage? image = obj as UIKit.UIImage;
                        if (image is null && obj is Foundation.NSObject nso)
                        {
                            var cast = ObjCRuntime.Runtime.GetNSObject(nso.Handle) as UIKit.UIImage;
                            if (cast is not null)
                            {
                                Diag(_logger, "LoadObject: cast via GetNSObject(UIImage) succeeded.");
                                image = cast;
                            }
                        }

                        if (image is null)
                        {
                            Diag(_logger, $"LoadObject could not cast to UIImage (type={obj.GetType().FullName}) — trying LoadDataRepresentation.");
                            TryLoadDataRepresentations(provider, 0);
                            return;
                        }

                        EncodeAndFinish(image, "LoadObject-UIImage");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "{Prefix} LoadObject handler failed.", LogPrefix);
                        Diag(_logger, $"LoadObject handler exception: {ex}");
                        _tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Fail("Photo import failed."));
                    }
                }

                // UIImage / UIKit work should run on main thread.
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(Run);
            });
        }

        private void TryLoadDataRepresentations(Foundation.NSItemProvider provider, int index)
        {
            var types = new[]
            {
                UniformTypeIdentifiers.UTTypes.Jpeg.Identifier,
                UniformTypeIdentifiers.UTTypes.Png.Identifier,
                UniformTypeIdentifiers.UTTypes.Heic.Identifier,
                "public.image"
            };

            if (index >= types.Length)
            {
                Diag(_logger, "All import strategies failed.");
                _tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Fail(
                    "Could not read image data. See debug output for [OCR PhotoLibrary] lines."));
                return;
            }

            var uti = types[index];
            Diag(_logger, $"LoadDataRepresentation attempt [{index}] UTI={uti}");

            provider.LoadDataRepresentation(uti, (data, err) =>
            {
                if (err is not null)
                {
                    Diag(_logger, $"LoadDataRepresentation UTI={uti} error: {err.LocalizedDescription}");
                    TryLoadDataRepresentations(provider, index + 1);
                    return;
                }

                if (data is null || data.Length == 0)
                {
                    Diag(_logger, $"LoadDataRepresentation UTI={uti} empty data.");
                    TryLoadDataRepresentations(provider, index + 1);
                    return;
                }

                Diag(_logger, $"LoadDataRepresentation UTI={uti} bytes={data.Length}");

                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        var bytes = data.ToArray();
                        // Already on main thread — decode can run inline.
                        FinalizeBytes(bytes, $"data:{uti}", mainThreadDecode: false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "{Prefix} LoadDataRepresentation finalize failed.", LogPrefix);
                        TryLoadDataRepresentations(provider, index + 1);
                    }
                });
            });
        }

        /// <summary>Normalize arbitrary image bytes to JPEG in quarantine.</summary>
        private void FinalizeBytes(byte[] bytes, string sourceTag, bool mainThreadDecode)
        {
            Diag(_logger, $"FinalizeBytes ({sourceTag}): rawLen={bytes.Length}, mainThreadDecode={mainThreadDecode}");

            if (bytes.Length > DocumentValidationPolicy.MaxFileSizeBytes)
            {
                _tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Fail(
                    $"Image exceeds maximum allowed size of {DocumentValidationPolicy.MaxFileSizeBytes / (1024 * 1024)} MB."));
                return;
            }

            // Already JPEG?
            var header = bytes.AsSpan(0, Math.Min(8, bytes.Length));
            if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            {
                var (ok, reason) = DocumentValidationPolicy.Validate(header, ".jpg", bytes.Length);
                if (ok)
                {
                    WriteQuarantineJpeg(bytes, sourceTag + "|native-jpeg");
                    return;
                }

                Diag(_logger, $"JPEG magic present but validation failed: {reason}");
            }

            void DecodeAndEncode()
            {
                // PNG / HEIC / TIFF: decode via UIImage then re-encode JPEG (UIKit on main thread).
                using var ns = Foundation.NSData.FromArray(bytes);
                using var image = UIKit.UIImage.LoadFromData(ns);
                if (image is null)
                {
                    Diag(_logger, $"UIImage.LoadFromData failed ({sourceTag}).");
                    _tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Fail("Could not decode image data."));
                    return;
                }

                EncodeAndFinish(image, sourceTag + "|decode");
            }

            if (mainThreadDecode)
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(DecodeAndEncode);
            else
                DecodeAndEncode();
        }

        private void EncodeAndFinish(UIKit.UIImage image, string sourceTag)
        {
            using var jpegData = image.AsJPEG(0.92f);
            if (jpegData is null || jpegData.Length == 0)
            {
                Diag(_logger, $"AsJPEG returned empty ({sourceTag}).");
                _tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Fail("Could not encode image as JPEG."));
                return;
            }

            var bytes = jpegData.ToArray();
            Diag(_logger, $"JPEG encoded ({sourceTag}): len={bytes.Length}");

            var header = bytes.AsSpan(0, Math.Min(8, bytes.Length));
            var (isValid, reason) = DocumentValidationPolicy.Validate(header, ".jpg", bytes.Length);
            if (!isValid)
            {
                Diag(_logger, $"JPEG validation failed: {reason}");
                _tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Fail(reason!));
                return;
            }

            WriteQuarantineJpeg(bytes, sourceTag);
        }

        private void WriteQuarantineJpeg(byte[] bytes, string sourceTag)
        {
            try
            {
                var opaque = $"gallery_{Guid.NewGuid():N}.jpg";
                var destPath = _vault.QuarantinePath(opaque);
                File.WriteAllBytes(destPath, bytes);
                _protection.ApplyCompleteProtection(destPath);
                Diag(_logger, $"SUCCESS — quarantine written: {opaque} ({sourceTag})");
                _tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Ok((destPath, DocumentMimeType.Jpeg)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Write quarantine failed.", LogPrefix);
                Diag(_logger, $"WriteQuarantineJpeg exception: {ex}");
                _tcs.TrySetResult(OcrResult<(string, DocumentMimeType)>.Fail("Photo import failed."));
            }
        }
    }
#else
    public Task<OcrResult<(string QuarantinePath, DocumentMimeType MimeType)>> PickAndImportAsync(CancellationToken ct = default)
        => Task.FromResult(OcrResult<(string, DocumentMimeType)>.Fail("Photo library import is only available on iOS/macCatalyst."));
#endif
}
