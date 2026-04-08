using System;
using System.Threading;
using System.Threading.Tasks;
using DigitalTwin.OCR.Models;

namespace DigitalTwin.Services;

public interface IDocumentPreviewService
{
    /// <summary>
    /// Presents a secure, in-app preview for the given document bytes and waits until
    /// the user dismisses the preview UI.
    /// </summary>
    Task<OcrResult<bool>> PreviewAsync(
        Guid documentId,
        string mimeType,
        byte[] plaintextBytes,
        CancellationToken ct = default);
}

