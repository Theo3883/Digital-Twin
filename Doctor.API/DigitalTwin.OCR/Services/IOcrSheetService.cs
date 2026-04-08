using DigitalTwin.OCR.Models;

namespace DigitalTwin.OCR.Services;

public interface IOcrSheetService
{
    /// <summary>
    /// Opens the OCR sheet overlay, waits for completion, and returns the result.
    /// Never throws — all OCR errors are captured in <see cref="OcrSessionResult"/>.
    /// </summary>
    Task<OcrSessionResult> LaunchAsync(OcrSessionRequest request, CancellationToken ct = default);

    /// <summary>Programmatically dismisses the sheet with a Cancelled result.</summary>
    void Dismiss();

    /// <summary>Hot observable — true while the sheet is visible.</summary>
    IObservable<bool> IsVisible { get; }
}
