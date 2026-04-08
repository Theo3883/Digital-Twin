using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Integrations.AI;

namespace DigitalTwin.Integrations.Medication;

/// <summary>
/// Uses Gemini with low temperature to resolve medication names to RxCUI (RxNorm) codes.
/// </summary>
public class GeminiRxCuiLookupProvider : IRxCuiLookupProvider
{
    private readonly IGeminiApiClient _gemini;

    private const string SystemPrompt = """
        You are a medical data lookup assistant. Your ONLY task is to return RxCUI (RxNorm Concept Unique Identifier) codes.
        RULES:
        - Respond with ONLY the numeric RxCUI code (e.g. 5640 for Ibuprofen). No explanation, no quotes, no extra text.
        - If you cannot identify the medication or are uncertain, respond with exactly: NONE
        - For brand names, return the RxCUI of the generic equivalent when known (e.g. Nurofen -> Ibuprofen's code).
        - Do not reason or elaborate. Output a single value only.
        """;

    public GeminiRxCuiLookupProvider(IGeminiApiClient gemini)
    {
        _gemini = gemini;
    }

    public async Task<string?> LookupRxCuiAsync(string medicationName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(medicationName)) return null;

        var userMessage = $"Return the RxCUI for: {medicationName.Trim()}";
        var response = await _gemini.GenerateContentAsync(SystemPrompt, userMessage, temperature: 0, ct);

        var trimmed = response.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Equals("NONE", StringComparison.OrdinalIgnoreCase))
            return null;

        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        return digits.Length > 0 ? digits : null;
    }
}
