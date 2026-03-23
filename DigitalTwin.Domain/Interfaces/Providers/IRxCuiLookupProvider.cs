namespace DigitalTwin.Domain.Interfaces.Providers;

/// <summary>
/// Resolves a medication name to its RxCUI (RxNorm code) for drug interaction checking.
/// </summary>
public interface IRxCuiLookupProvider
{
    /// <summary>
    /// Looks up the RxCUI for a medication by name. Returns null if not found.
    /// </summary>
    Task<string?> LookupRxCuiAsync(string medicationName, CancellationToken ct = default);
}
