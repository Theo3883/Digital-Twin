namespace DigitalTwin.OCR.Models;

/// <summary>Minimal discriminated result type — avoids exception-based control flow across the OCR boundary.</summary>
public readonly struct OcrResult<T>
{
    private OcrResult(T? value, string? error)
    {
        Value = value;
        Error = error;
        IsSuccess = error is null;
    }

    public T? Value { get; }
    public string? Error { get; }
    public bool IsSuccess { get; }

    public static OcrResult<T> Ok(T value) => new(value, null);
    public static OcrResult<T> Fail(string error) => new(default, error);

    public override string ToString() => IsSuccess ? $"Ok({Value})" : $"Fail({Error})";
}
