using DigitalTwin.Domain.Interfaces.Providers;

namespace DigitalTwin.Integrations.Medication;

/// <summary>
/// No-op implementation when Gemini AI is not configured.
/// </summary>
public class NullRxCuiLookupProvider : IRxCuiLookupProvider
{
    public Task<string?> LookupRxCuiAsync(string medicationName, CancellationToken ct = default)
        => Task.FromResult<string?>(null);
}
