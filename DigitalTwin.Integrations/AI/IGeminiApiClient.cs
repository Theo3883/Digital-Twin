namespace DigitalTwin.Integrations.AI;

/// <summary>
/// Abstracts Gemini REST API communication. Single responsibility: send a
/// system prompt + user message and return the generated text.
/// </summary>
public interface IGeminiApiClient
{
    Task<string> GenerateContentAsync(string systemPrompt, string userMessage, CancellationToken ct = default);

    /// <summary>
    /// Same as GenerateContentAsync but with a temperature override (e.g. 0 for deterministic lookup tasks).
    /// </summary>
    Task<string> GenerateContentAsync(
        string systemPrompt,
        string userMessage,
        double temperature,
        CancellationToken ct = default);
}
