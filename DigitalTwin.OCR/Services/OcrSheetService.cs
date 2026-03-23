using System.Reactive.Subjects;
using DigitalTwin.OCR.Models;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Singleton service that coordinates sheet visibility and the async OCR session lifecycle.
/// The <see cref="OcrSheet"/> component subscribes to <see cref="IsVisible"/> and renders
/// its content accordingly.
/// </summary>
public sealed class OcrSheetService : IOcrSheetService, IDisposable
{
    private readonly BehaviorSubject<bool> _isVisible = new(false);
    private TaskCompletionSource<OcrSessionResult>? _tcs;
    private OcrSessionRequest? _currentRequest;

    public IObservable<bool> IsVisible => _isVisible;
    public OcrSessionRequest? CurrentRequest => _currentRequest;

    public Task<OcrSessionResult> LaunchAsync(OcrSessionRequest request, CancellationToken ct = default)
    {
        _currentRequest = request;
        _tcs = new TaskCompletionSource<OcrSessionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _isVisible.OnNext(true);

        ct.Register(() =>
        {
            if (_tcs?.Task.IsCompleted == false)
                Complete(new OcrSessionResult(OcrSessionStatus.Cancelled));
        });

        return _tcs.Task;
    }

    public void Dismiss() => Complete(new OcrSessionResult(OcrSessionStatus.Cancelled));

    /// <summary>Called by OcrSheet when the OCR session finishes (success or error).</summary>
    public void Complete(OcrSessionResult result)
    {
        _isVisible.OnNext(false);
        _currentRequest = null;
        _tcs?.TrySetResult(result);
        _tcs = null;
    }

    public void Dispose()
    {
        _isVisible.OnCompleted();
        _isVisible.Dispose();
    }
}
