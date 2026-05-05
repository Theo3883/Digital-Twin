namespace DigitalTwin.Mobile.OCR.Models;

/// <summary>
/// Minimal discriminated result type — avoids exception-based control flow across OCR boundary.
/// </summary>
public readonly struct OcrResult<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public bool IsSuccess { get; }

    private OcrResult(T? value, string? error, bool isSuccess)
    {
        Value = value;
        Error = error;
        IsSuccess = isSuccess;
    }

    public static OcrResult<T> Ok(T value) => new(value, null, true);
    public static OcrResult<T> Fail(string error) => new(default, error, false);
}
