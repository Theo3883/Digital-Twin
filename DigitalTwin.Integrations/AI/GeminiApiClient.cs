using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DigitalTwin.Integrations.AI;

/// <summary>
/// Facade for Gemini REST API. Single responsibility: HTTP communication.
/// </summary>
public class GeminiApiClient : IGeminiApiClient
{
    private readonly HttpClient _httpClient;
    private readonly GeminiPromptOptions _options;
    private readonly ILogger<GeminiApiClient> _logger;
    private readonly string _apiKey;

    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-lite:generateContent";

    public GeminiApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<GeminiPromptOptions> options,
        ILogger<GeminiApiClient> logger,
        string apiKey)
    {
        _httpClient = httpClientFactory.CreateClient("GeminiApi");
        _options    = options.Value;
        _logger     = logger;
        _apiKey     = apiKey;
    }

    public Task<string> GenerateContentAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
        => GenerateContentAsync(systemPrompt, userMessage, _options.Temperature, ct);

    public async Task<string> GenerateContentAsync(
        string systemPrompt,
        string userMessage,
        double temperature,
        CancellationToken ct = default)
    {
        var requestBody = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role  = "user",
                    parts = new[] { new { text = userMessage } }
                }
            },
            generationConfig = new
            {
                temperature     = temperature,
                topP            = _options.TopP,
                topK            = _options.TopK,
                maxOutputTokens = _options.MaxOutputTokens,
                candidateCount  = 1
            }
        };

        var url = $"{BaseUrl}?key={_apiKey}";

        for (var attempt = 1; attempt <= _options.MaxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            _logger.LogInformation("[GeminiApi] Sending request (attempt {Attempt}/{Max}).", attempt, _options.MaxRetries);

            var response = await _httpClient.PostAsJsonAsync(url, requestBody, ct);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                return ExtractTextFromResponse(json);
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);

            if ((int)response.StatusCode == 429 && attempt < _options.MaxRetries)
            {
                var delay = ParseRetryDelay(errorBody);
                _logger.LogWarning("[GeminiApi] Rate limited (429). Waiting {Delay}s before retry {Next}/{Max}.",
                    delay.TotalSeconds, attempt + 1, _options.MaxRetries);
                await Task.Delay(delay, ct);
                continue;
            }

            _logger.LogError("[GeminiApi] API returned {StatusCode}: {Body}", response.StatusCode, errorBody);

            if ((int)response.StatusCode == 429)
                return "The AI assistant is temporarily unavailable due to rate limits. Please try again in a minute.";

            return "I'm sorry, something went wrong. Please try again.";
        }

        return "The AI assistant is temporarily unavailable. Please try again shortly.";
    }

    private string ExtractTextFromResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    return parts[0].GetProperty("text").GetString() ?? string.Empty;
                }
            }

            _logger.LogWarning("[GeminiApi] Unexpected response structure. Raw: {Json}", json);
            return "I'm sorry, I wasn't able to process that request. Please try again.";
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[GeminiApi] Failed to parse response JSON.");
            return "I'm sorry, I wasn't able to process that request. Please try again.";
        }
    }

    private TimeSpan ParseRetryDelay(string errorBody)
    {
        // Gemini returns retryDelay as e.g. "55s" inside the error details.
        try
        {
            using var doc = JsonDocument.Parse(errorBody);
            var details = doc.RootElement
                .GetProperty("error")
                .GetProperty("details");

            foreach (var detail in details.EnumerateArray())
            {
                if (detail.TryGetProperty("retryDelay", out var retryDelay))
                {
                    var raw = retryDelay.GetString() ?? string.Empty;
                    // Format is "55s" or "55.414774484s"
                    if (raw.EndsWith('s') &&
                        double.TryParse(raw.TrimEnd('s'),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var seconds))
                    {
                        // Cap at MaxRetryDelay to avoid blocking the UI for too long.
                        return TimeSpan.FromSeconds(Math.Min(seconds, _options.MaxRetryDelaySeconds));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GeminiApi] Could not parse retryDelay from error response.");
        }

        return TimeSpan.FromSeconds(_options.DefaultRetryDelaySeconds);
    }
}
