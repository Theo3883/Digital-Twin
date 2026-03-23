using DigitalTwin.Domain.Interfaces.Providers;

namespace DigitalTwin.Integrations.Medication;

/// <summary>
/// Decorator that chains two <see cref="IRxCuiLookupProvider"/> implementations.
/// The primary provider (<see cref="RxNavRxCuiResolver"/>) is tried first because
/// it uses the authoritative RxNorm database. The fallback (Gemini AI or Null) is
/// only called when the primary returns <c>null</c>, handling drugs not yet indexed
/// in RxNorm (very new drugs, non-US brand names, etc.).
/// </summary>
public sealed class ChainedRxCuiLookupProvider : IRxCuiLookupProvider
{
    private readonly RxNavRxCuiResolver _primary;
    private readonly IRxCuiLookupProvider _fallback;

    public ChainedRxCuiLookupProvider(RxNavRxCuiResolver primary, IRxCuiLookupProvider fallback)
    {
        _primary  = primary;
        _fallback = fallback;
    }

    public async Task<string?> LookupRxCuiAsync(string medicationName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(medicationName))
            return null;

        var rxCui = await _primary.LookupRxCuiAsync(medicationName, ct);
        if (!string.IsNullOrWhiteSpace(rxCui))
            return rxCui;

        return await _fallback.LookupRxCuiAsync(medicationName, ct);
    }
}
