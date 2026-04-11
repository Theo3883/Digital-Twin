using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalTwin.Mobile.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Infrastructure.Services;

internal static class GeminiRetryPolicy
{
    internal const int MaxAttempts = 3;
    internal static readonly SemaphoreSlim RequestGate = new(1, 1);

    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    internal static bool ShouldRetry(HttpStatusCode statusCode, int attempt)
        => statusCode == HttpStatusCode.TooManyRequests && attempt < MaxAttempts;

    internal static TimeSpan GetRetryDelay(string errorBody, ILogger logger, string requestId)
    {
        var parsed = TryParseRetryDelay(errorBody, logger, requestId);
        var baseDelay = parsed ?? DefaultRetryDelay;
        var delayWithJitter = baseDelay.Add(TimeSpan.FromMilliseconds(Random.Shared.Next(100, 1000)));
        return delayWithJitter > MaxRetryDelay ? MaxRetryDelay : delayWithJitter;
    }

    internal static bool IsHardQuotaExceeded(string errorBody, ILogger logger, string requestId)
    {
        if (string.IsNullOrWhiteSpace(errorBody))
            return false;

        // Fast path for known Gemini quota-exhausted messages.
        var normalized = errorBody.ToLowerInvariant();
        if (normalized.Contains("exceeded your current quota") ||
            normalized.Contains("check your plan and billing details") ||
            (normalized.Contains("requests per day") && normalized.Contains("is 0")))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(errorBody);
            if (!document.RootElement.TryGetProperty("error", out var error))
                return false;

            if (error.TryGetProperty("message", out var messageElement) &&
                messageElement.ValueKind == JsonValueKind.String)
            {
                var message = messageElement.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    var normalizedMessage = message.ToLowerInvariant();
                    if (normalizedMessage.Contains("exceeded your current quota") ||
                        normalizedMessage.Contains("billing details") ||
                        (normalizedMessage.Contains("requests per day") && normalizedMessage.Contains("is 0")))
                    {
                        return true;
                    }
                }
            }

            if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
            {
                foreach (var detail in details.EnumerateArray())
                {
                    if (!detail.TryGetProperty("violations", out var violations) || violations.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var violation in violations.EnumerateArray())
                    {
                        if (!violation.TryGetProperty("description", out var description) ||
                            description.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }

                        var descriptionText = description.GetString();
                        if (string.IsNullOrWhiteSpace(descriptionText))
                            continue;

                        var normalizedDescription = descriptionText.ToLowerInvariant();
                        if (normalizedDescription.Contains("requests per day") && normalizedDescription.Contains("is 0"))
                            return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[Gemini][{RequestId}] Failed to parse quota exhaustion metadata from error payload.", requestId);
        }

        return false;
    }

    internal static string TrimForLog(string errorBody)
    {
        const int maxLength = 600;
        if (string.IsNullOrEmpty(errorBody))
            return string.Empty;

        return errorBody.Length <= maxLength
            ? errorBody
            : string.Concat(errorBody.AsSpan(0, maxLength), "...");
    }

    internal static string ResolveCorrelationId()
    {
        var traceId = Activity.Current?.TraceId.ToString();
        return string.IsNullOrWhiteSpace(traceId)
            ? Guid.NewGuid().ToString("N")[..8]
            : traceId;
    }

    private static TimeSpan? TryParseRetryDelay(string errorBody, ILogger logger, string requestId)
    {
        if (string.IsNullOrWhiteSpace(errorBody))
            return null;

        try
        {
            using var document = JsonDocument.Parse(errorBody);
            if (!document.RootElement.TryGetProperty("error", out var error) ||
                !error.TryGetProperty("details", out var details) ||
                details.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var detail in details.EnumerateArray())
            {
                if (!detail.TryGetProperty("retryDelay", out var retryDelayElement) ||
                    retryDelayElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var raw = retryDelayElement.GetString();
                if (string.IsNullOrWhiteSpace(raw) || !raw.EndsWith('s'))
                    continue;

                if (!double.TryParse(raw.TrimEnd('s'), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                    continue;

                if (seconds <= 0)
                    continue;

                return TimeSpan.FromSeconds(seconds);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[Gemini][{RequestId}] Failed to parse retryDelay from error payload.", requestId);
        }

        return null;
    }
}

public class GeminiChatService : IChatBotProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiChatService> _logger;
    private readonly string _apiKey;

    public GeminiChatService(HttpClient httpClient, string apiKey, ILogger<GeminiChatService> logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _logger = logger;
    }

    public async Task<string> SendMessageAsync(string userMessage, string? patientContext, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return "AI assistant is not configured. Please set up your Gemini API key.";

        var requestId = GeminiRetryPolicy.ResolveCorrelationId();
        var gateHeld = false;

        try
        {
            await GeminiRetryPolicy.RequestGate.WaitAsync(ct);
            gateHeld = true;

            var systemPrompt = "You are a helpful medical assistant for a digital twin health app. " +
                "Provide general health guidance. Never diagnose conditions.";
            if (!string.IsNullOrEmpty(patientContext))
                systemPrompt += $"\n\nPatient context: {patientContext}";

            var request = new GeminiRequest
            {
                SystemInstruction = new GeminiContent
                {
                    Parts = [new GeminiPart { Text = systemPrompt }]
                },
                Contents =
                [
                    new GeminiContentEntry
                    {
                        Role = "user",
                        Parts = [new GeminiPart { Text = userMessage }]
                    }
                ],
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = 0.7f,
                    TopP = 0.9f,
                    TopK = 40,
                    MaxOutputTokens = 2048
                }
            };

            var url = $"?key={_apiKey}";

            for (var attempt = 1; attempt <= GeminiRetryPolicy.MaxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                _logger.LogInformation(
                    "[Gemini][{RequestId}] Chat request attempt {Attempt}/{MaxAttempts}.",
                    requestId,
                    attempt,
                    GeminiRetryPolicy.MaxAttempts);

                using var content = new StringContent(
                    JsonSerializer.Serialize(request, IntegrationJsonContext.Default.GeminiRequest),
                    System.Text.Encoding.UTF8,
                    "application/json");

                using var response = await _httpClient.PostAsync(url, content, ct);

                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(ct);
                    var geminiResponse = await JsonSerializer.DeserializeAsync(stream, IntegrationJsonContext.Default.GeminiResponse, ct);
                    var text = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

                    if (!string.IsNullOrWhiteSpace(text))
                        return text;

                    _logger.LogWarning("[Gemini][{RequestId}] Chat response missing candidate text.", requestId);
                    return "I wasn't able to generate a response. Please try again.";
                }

                var errorBody = await response.Content.ReadAsStringAsync(ct);

                if (response.StatusCode == HttpStatusCode.TooManyRequests &&
                    GeminiRetryPolicy.IsHardQuotaExceeded(errorBody, _logger, requestId))
                {
                    _logger.LogWarning(
                        "[Gemini][{RequestId}] Chat request hit hard quota exhaustion. Skipping retries. Body: {ErrorBody}",
                        requestId,
                        GeminiRetryPolicy.TrimForLog(errorBody));
                    return "The AI assistant quota is exhausted right now. Please try again later.";
                }

                if (GeminiRetryPolicy.ShouldRetry(response.StatusCode, attempt))
                {
                    var delay = GeminiRetryPolicy.GetRetryDelay(errorBody, _logger, requestId);
                    _logger.LogWarning(
                        "[Gemini][{RequestId}] Chat request got {StatusCode}. Retrying in {DelaySeconds:F1}s (next attempt {NextAttempt}/{MaxAttempts}).",
                        requestId,
                        (int)response.StatusCode,
                        delay.TotalSeconds,
                        attempt + 1,
                        GeminiRetryPolicy.MaxAttempts);

                    await Task.Delay(delay, ct);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning(
                        "[Gemini][{RequestId}] Chat request rate limited after {MaxAttempts} attempts. Returning degraded response. Body: {ErrorBody}",
                        requestId,
                        GeminiRetryPolicy.MaxAttempts,
                        GeminiRetryPolicy.TrimForLog(errorBody));
                    return "The AI assistant is temporarily rate limited. Please try again in a minute.";
                }

                _logger.LogError(
                    "[Gemini][{RequestId}] Chat request failed with status {StatusCode}. Body: {ErrorBody}",
                    requestId,
                    (int)response.StatusCode,
                    GeminiRetryPolicy.TrimForLog(errorBody));
                return "I'm having trouble connecting to the AI service right now.";
            }

            _logger.LogWarning(
                "[Gemini][{RequestId}] Chat request exhausted retries unexpectedly. Returning degraded response.",
                requestId);
            return "The AI assistant is temporarily rate limited. Please try again in a minute.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Gemini][{RequestId}] Chat request exception", requestId);
            return "An error occurred while communicating with the AI assistant.";
        }
        finally
        {
            if (gateHeld)
                GeminiRetryPolicy.RequestGate.Release();
        }
    }
}

public class GeminiCoachingService : ICoachingProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiCoachingService> _logger;
    private readonly string _apiKey;

    public GeminiCoachingService(HttpClient httpClient, string apiKey, ILogger<GeminiCoachingService> logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _logger = logger;
    }

    public async Task<string> GetAdviceAsync(string patientContext, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return "Stay hydrated, get regular exercise, and maintain a balanced diet.";

        var requestId = GeminiRetryPolicy.ResolveCorrelationId();
        var gateHeld = false;

        try
        {
            await GeminiRetryPolicy.RequestGate.WaitAsync(ct);
            gateHeld = true;

            var systemPrompt = "You are a health coaching assistant. Provide personalized, " +
                "actionable wellness advice based on the patient's context. Keep advice practical and encouraging.";

            var request = new GeminiRequest
            {
                SystemInstruction = new GeminiContent
                {
                    Parts = [new GeminiPart { Text = systemPrompt }]
                },
                Contents =
                [
                    new GeminiContentEntry
                    {
                        Role = "user",
                        Parts = [new GeminiPart { Text = $"Please provide health coaching advice for this patient:\n{patientContext}" }]
                    }
                ],
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = 0.8f,
                    TopP = 0.9f,
                    TopK = 40,
                    MaxOutputTokens = 1024
                }
            };

            var url = $"?key={_apiKey}";

            for (var attempt = 1; attempt <= GeminiRetryPolicy.MaxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                _logger.LogInformation(
                    "[Gemini][{RequestId}] Coaching request attempt {Attempt}/{MaxAttempts}.",
                    requestId,
                    attempt,
                    GeminiRetryPolicy.MaxAttempts);

                using var content = new StringContent(
                    JsonSerializer.Serialize(request, IntegrationJsonContext.Default.GeminiRequest),
                    System.Text.Encoding.UTF8,
                    "application/json");

                using var response = await _httpClient.PostAsync(url, content, ct);

                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(ct);
                    var geminiResponse = await JsonSerializer.DeserializeAsync(stream, IntegrationJsonContext.Default.GeminiResponse, ct);
                    var text = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
                    return !string.IsNullOrWhiteSpace(text)
                        ? text
                        : "Stay hydrated, get regular exercise, and maintain a balanced diet.";
                }

                var errorBody = await response.Content.ReadAsStringAsync(ct);

                if (response.StatusCode == HttpStatusCode.TooManyRequests &&
                    GeminiRetryPolicy.IsHardQuotaExceeded(errorBody, _logger, requestId))
                {
                    _logger.LogWarning(
                        "[Gemini][{RequestId}] Coaching request hit hard quota exhaustion. Skipping retries. Body: {ErrorBody}",
                        requestId,
                        GeminiRetryPolicy.TrimForLog(errorBody));
                    return "AI coaching quota is exhausted right now. Please try again later.";
                }

                if (GeminiRetryPolicy.ShouldRetry(response.StatusCode, attempt))
                {
                    var delay = GeminiRetryPolicy.GetRetryDelay(errorBody, _logger, requestId);
                    _logger.LogWarning(
                        "[Gemini][{RequestId}] Coaching request got {StatusCode}. Retrying in {DelaySeconds:F1}s (next attempt {NextAttempt}/{MaxAttempts}).",
                        requestId,
                        (int)response.StatusCode,
                        delay.TotalSeconds,
                        attempt + 1,
                        GeminiRetryPolicy.MaxAttempts);

                    await Task.Delay(delay, ct);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning(
                        "[Gemini][{RequestId}] Coaching request rate limited after {MaxAttempts} attempts. Returning fallback. Body: {ErrorBody}",
                        requestId,
                        GeminiRetryPolicy.MaxAttempts,
                        GeminiRetryPolicy.TrimForLog(errorBody));
                    return "AI coaching is temporarily rate limited. Please try again in a minute.";
                }

                _logger.LogError(
                    "[Gemini][{RequestId}] Coaching request failed with status {StatusCode}. Body: {ErrorBody}",
                    requestId,
                    (int)response.StatusCode,
                    GeminiRetryPolicy.TrimForLog(errorBody));
                return "Stay hydrated, get regular exercise, and maintain a balanced diet.";
            }

            _logger.LogWarning(
                "[Gemini][{RequestId}] Coaching request exhausted retries unexpectedly. Returning fallback.",
                requestId);
            return "AI coaching is temporarily rate limited. Please try again in a minute.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Gemini][{RequestId}] Coaching request exception", requestId);
            return "Stay hydrated, get regular exercise, and maintain a balanced diet.";
        }
        finally
        {
            if (gateHeld)
                GeminiRetryPolicy.RequestGate.Release();
        }
    }
}

// Gemini API JSON models
public sealed record GeminiRequest
{
    [JsonPropertyName("system_instruction")]
    public GeminiContent? SystemInstruction { get; init; }

    [JsonPropertyName("contents")]
    public List<GeminiContentEntry> Contents { get; init; } = [];

    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig? GenerationConfig { get; init; }
}

public sealed record GeminiContent
{
    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; init; } = [];
}

public sealed record GeminiContentEntry
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; init; } = [];
}

public sealed record GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;
}

public sealed record GeminiGenerationConfig
{
    [JsonPropertyName("temperature")]
    public float Temperature { get; init; }

    [JsonPropertyName("topP")]
    public float TopP { get; init; }

    [JsonPropertyName("topK")]
    public int TopK { get; init; }

    [JsonPropertyName("maxOutputTokens")]
    public int MaxOutputTokens { get; init; }
}

public sealed record GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; init; }
}

public sealed record GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; init; }
}
