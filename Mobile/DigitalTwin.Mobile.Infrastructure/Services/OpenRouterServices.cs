using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalTwin.Mobile.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Infrastructure.Services;

internal static class OpenRouterRetryPolicy
{
    internal const int MaxAttempts = 3;
    internal static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(75);

    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(15);

    internal static bool ShouldRetry(HttpStatusCode statusCode, int attempt)
        => attempt < MaxAttempts &&
           (statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500);

    internal static TimeSpan GetRetryDelay(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is TimeSpan retryAfter && retryAfter > TimeSpan.Zero)
            return retryAfter > MaxRetryDelay ? MaxRetryDelay : retryAfter;

        var delayWithJitter = DefaultRetryDelay.Add(TimeSpan.FromMilliseconds(Random.Shared.Next(100, 700)));
        return delayWithJitter > MaxRetryDelay ? MaxRetryDelay : delayWithJitter;
    }

    internal static TimeSpan GetTimeoutRetryDelay(int attempt)
    {
        var baseSeconds = Math.Clamp(attempt * 2, 2, 8);
        return TimeSpan.FromSeconds(baseSeconds)
            .Add(TimeSpan.FromMilliseconds(Random.Shared.Next(100, 700)));
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

    internal static bool IsTimeoutCancellation(CancellationToken callerToken)
        => !callerToken.IsCancellationRequested;
}

public sealed class OpenRouterChatService : IChatBotProvider
{
    internal const string DefaultModel = "openai/gpt-oss-20b";

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouterChatService> _logger;
    private readonly string _apiKey;
    private readonly string _model;

    public OpenRouterChatService(HttpClient httpClient, string apiKey, string? model, ILogger<OpenRouterChatService> logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();
        _logger = logger;
    }

    public async Task<string> SendMessageAsync(string userMessage, string? patientContext, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return "AI fallback is not configured. Please set up your OpenRouter API key.";

        var requestId = GeminiRetryPolicy.ResolveCorrelationId();
        var systemPrompt = "You are a helpful medical assistant for a digital twin health app. " +
            "Provide general health guidance. Never diagnose conditions.";
        if (!string.IsNullOrEmpty(patientContext))
            systemPrompt += $"\n\nPatient context: {patientContext}";

        var request = new OpenRouterChatRequest
        {
            Model = _model,
            Messages =
            [
                new OpenRouterChatMessage { Role = "system", Content = systemPrompt },
                new OpenRouterChatMessage { Role = "user", Content = userMessage }
            ],
            Temperature = 0.7f,
            MaxTokens = 2048
        };

        try
        {
            for (var attempt = 1; attempt <= OpenRouterRetryPolicy.MaxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                var stopwatch = Stopwatch.StartNew();
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                attemptCts.CancelAfter(OpenRouterRetryPolicy.AttemptTimeout);
                var attemptToken = attemptCts.Token;

                _logger.LogInformation(
                    "[OpenRouter][{RequestId}] Chat request attempt {Attempt}/{MaxAttempts} (timeout {TimeoutSeconds}s).",
                    requestId,
                    attempt,
                    OpenRouterRetryPolicy.MaxAttempts,
                    OpenRouterRetryPolicy.AttemptTimeout.TotalSeconds);

                try
                {
                    using var httpRequest = new HttpRequestMessage(HttpMethod.Post, string.Empty)
                    {
                        Content = new StringContent(
                            JsonSerializer.Serialize(request, IntegrationJsonContext.Default.OpenRouterChatRequest),
                            Encoding.UTF8,
                            "application/json")
                    };
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                    using var response = await _httpClient.SendAsync(
                        httpRequest,
                        HttpCompletionOption.ResponseHeadersRead,
                        attemptToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseBody = await response.Content.ReadAsStringAsync(attemptToken);
                        var openRouterResponse = JsonSerializer.Deserialize(responseBody, IntegrationJsonContext.Default.OpenRouterChatResponse);
                        var text = openRouterResponse?.Choices?.FirstOrDefault()?.Message?.Content;

                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            _logger.LogInformation(
                                "[OpenRouter][{RequestId}] Chat request completed in {ElapsedMs}ms (body {BodyLength} chars).",
                                requestId,
                                stopwatch.ElapsedMilliseconds,
                                responseBody.Length);
                            return text;
                        }

                        _logger.LogWarning("[OpenRouter][{RequestId}] Chat response missing message content.", requestId);
                        return "I wasn't able to generate a response. Please try again.";
                    }

                    var errorBody = await response.Content.ReadAsStringAsync(attemptToken);
                    if (OpenRouterRetryPolicy.ShouldRetry(response.StatusCode, attempt))
                    {
                        var delay = OpenRouterRetryPolicy.GetRetryDelay(response);
                        _logger.LogWarning(
                            "[OpenRouter][{RequestId}] Chat request got {StatusCode} after {ElapsedMs}ms. Retrying in {DelaySeconds:F1}s (next attempt {NextAttempt}/{MaxAttempts}). Body: {ErrorBody}",
                            requestId,
                            (int)response.StatusCode,
                            stopwatch.ElapsedMilliseconds,
                            delay.TotalSeconds,
                            attempt + 1,
                            OpenRouterRetryPolicy.MaxAttempts,
                            OpenRouterRetryPolicy.TrimForLog(errorBody));

                        await Task.Delay(delay, ct);
                        continue;
                    }

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning(
                            "[OpenRouter][{RequestId}] Chat request rate limited after {ElapsedMs}ms. Body: {ErrorBody}",
                            requestId,
                            stopwatch.ElapsedMilliseconds,
                            OpenRouterRetryPolicy.TrimForLog(errorBody));
                        return "The AI assistant is temporarily rate limited. Please try again in a minute.";
                    }

                    _logger.LogError(
                        "[OpenRouter][{RequestId}] Chat request failed with status {StatusCode} after {ElapsedMs}ms. Body: {ErrorBody}",
                        requestId,
                        (int)response.StatusCode,
                        stopwatch.ElapsedMilliseconds,
                        OpenRouterRetryPolicy.TrimForLog(errorBody));
                    return "I'm having trouble connecting to the AI service right now.";
                }
                catch (OperationCanceledException) when (OpenRouterRetryPolicy.IsTimeoutCancellation(ct) && attempt < OpenRouterRetryPolicy.MaxAttempts)
                {
                    var delay = OpenRouterRetryPolicy.GetTimeoutRetryDelay(attempt);
                    _logger.LogWarning(
                        "[OpenRouter][{RequestId}] Chat request timed out after {ElapsedMs}ms. Retrying in {DelaySeconds:F1}s (next attempt {NextAttempt}/{MaxAttempts}).",
                        requestId,
                        stopwatch.ElapsedMilliseconds,
                        delay.TotalSeconds,
                        attempt + 1,
                        OpenRouterRetryPolicy.MaxAttempts);

                    await Task.Delay(delay, ct);
                    continue;
                }
                catch (OperationCanceledException) when (OpenRouterRetryPolicy.IsTimeoutCancellation(ct))
                {
                    throw;
                }
            }

            _logger.LogWarning(
                "[OpenRouter][{RequestId}] Chat request exhausted retries unexpectedly.",
                requestId);
            return "The AI assistant is temporarily rate limited. Please try again in a minute.";
        }
        catch (OperationCanceledException) when (OpenRouterRetryPolicy.IsTimeoutCancellation(ct))
        {
            _logger.LogWarning(
                "[OpenRouter][{RequestId}] Chat request timed out after {TimeoutSeconds}s and exhausted retries.",
                requestId,
                OpenRouterRetryPolicy.AttemptTimeout.TotalSeconds);
            return "The AI assistant is taking too long to respond right now. Please try again.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenRouter][{RequestId}] Chat request exception", requestId);
            return "An error occurred while communicating with the AI assistant.";
        }
    }
}

public sealed class OpenRouterCoachingService : ICoachingProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouterCoachingService> _logger;
    private readonly string _apiKey;
    private readonly string _model;

    public OpenRouterCoachingService(HttpClient httpClient, string apiKey, string? model, ILogger<OpenRouterCoachingService> logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? OpenRouterChatService.DefaultModel : model.Trim();
        _logger = logger;
    }

    public async Task<string> GetAdviceAsync(string patientContext, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return "Stay hydrated, get regular exercise, and maintain a balanced diet.";

        var requestId = GeminiRetryPolicy.ResolveCorrelationId();
        var request = new OpenRouterChatRequest
        {
            Model = _model,
            Messages =
            [
                new OpenRouterChatMessage
                {
                    Role = "system",
                    Content = "You are a health coaching assistant. Provide personalized, actionable wellness advice based on the patient's context. Keep advice practical and encouraging."
                },
                new OpenRouterChatMessage
                {
                    Role = "user",
                    Content = $"Please provide health coaching advice for this patient:\n{patientContext}"
                }
            ],
            Temperature = 0.8f,
            MaxTokens = 1024
        };

        try
        {
            for (var attempt = 1; attempt <= OpenRouterRetryPolicy.MaxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                var stopwatch = Stopwatch.StartNew();
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                attemptCts.CancelAfter(OpenRouterRetryPolicy.AttemptTimeout);
                var attemptToken = attemptCts.Token;

                _logger.LogInformation(
                    "[OpenRouter][{RequestId}] Coaching request attempt {Attempt}/{MaxAttempts} (timeout {TimeoutSeconds}s).",
                    requestId,
                    attempt,
                    OpenRouterRetryPolicy.MaxAttempts,
                    OpenRouterRetryPolicy.AttemptTimeout.TotalSeconds);

                try
                {
                    using var httpRequest = new HttpRequestMessage(HttpMethod.Post, string.Empty)
                    {
                        Content = new StringContent(
                            JsonSerializer.Serialize(request, IntegrationJsonContext.Default.OpenRouterChatRequest),
                            Encoding.UTF8,
                            "application/json")
                    };
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                    using var response = await _httpClient.SendAsync(
                        httpRequest,
                        HttpCompletionOption.ResponseHeadersRead,
                        attemptToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseBody = await response.Content.ReadAsStringAsync(attemptToken);
                        var openRouterResponse = JsonSerializer.Deserialize(responseBody, IntegrationJsonContext.Default.OpenRouterChatResponse);
                        var text = openRouterResponse?.Choices?.FirstOrDefault()?.Message?.Content;

                        _logger.LogInformation(
                            "[OpenRouter][{RequestId}] Coaching request completed in {ElapsedMs}ms (body {BodyLength} chars).",
                            requestId,
                            stopwatch.ElapsedMilliseconds,
                            responseBody.Length);

                        return !string.IsNullOrWhiteSpace(text)
                            ? text
                            : "Stay hydrated, get regular exercise, and maintain a balanced diet.";
                    }

                    var errorBody = await response.Content.ReadAsStringAsync(attemptToken);
                    if (OpenRouterRetryPolicy.ShouldRetry(response.StatusCode, attempt))
                    {
                        var delay = OpenRouterRetryPolicy.GetRetryDelay(response);
                        _logger.LogWarning(
                            "[OpenRouter][{RequestId}] Coaching request got {StatusCode} after {ElapsedMs}ms. Retrying in {DelaySeconds:F1}s (next attempt {NextAttempt}/{MaxAttempts}). Body: {ErrorBody}",
                            requestId,
                            (int)response.StatusCode,
                            stopwatch.ElapsedMilliseconds,
                            delay.TotalSeconds,
                            attempt + 1,
                            OpenRouterRetryPolicy.MaxAttempts,
                            OpenRouterRetryPolicy.TrimForLog(errorBody));

                        await Task.Delay(delay, ct);
                        continue;
                    }

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning(
                            "[OpenRouter][{RequestId}] Coaching request rate limited after {ElapsedMs}ms. Body: {ErrorBody}",
                            requestId,
                            stopwatch.ElapsedMilliseconds,
                            OpenRouterRetryPolicy.TrimForLog(errorBody));
                        return "AI coaching is temporarily rate limited. Please try again in a minute.";
                    }

                    _logger.LogError(
                        "[OpenRouter][{RequestId}] Coaching request failed with status {StatusCode} after {ElapsedMs}ms. Body: {ErrorBody}",
                        requestId,
                        (int)response.StatusCode,
                        stopwatch.ElapsedMilliseconds,
                        OpenRouterRetryPolicy.TrimForLog(errorBody));
                    return "Stay hydrated, get regular exercise, and maintain a balanced diet.";
                }
                catch (OperationCanceledException) when (OpenRouterRetryPolicy.IsTimeoutCancellation(ct) && attempt < OpenRouterRetryPolicy.MaxAttempts)
                {
                    var delay = OpenRouterRetryPolicy.GetTimeoutRetryDelay(attempt);
                    _logger.LogWarning(
                        "[OpenRouter][{RequestId}] Coaching request timed out after {ElapsedMs}ms. Retrying in {DelaySeconds:F1}s (next attempt {NextAttempt}/{MaxAttempts}).",
                        requestId,
                        stopwatch.ElapsedMilliseconds,
                        delay.TotalSeconds,
                        attempt + 1,
                        OpenRouterRetryPolicy.MaxAttempts);

                    await Task.Delay(delay, ct);
                    continue;
                }
                catch (OperationCanceledException) when (OpenRouterRetryPolicy.IsTimeoutCancellation(ct))
                {
                    throw;
                }
            }

            _logger.LogWarning(
                "[OpenRouter][{RequestId}] Coaching request exhausted retries unexpectedly.",
                requestId);
            return "AI coaching is temporarily rate limited. Please try again in a minute.";
        }
        catch (OperationCanceledException) when (OpenRouterRetryPolicy.IsTimeoutCancellation(ct))
        {
            _logger.LogWarning(
                "[OpenRouter][{RequestId}] Coaching request timed out after {TimeoutSeconds}s and exhausted retries.",
                requestId,
                OpenRouterRetryPolicy.AttemptTimeout.TotalSeconds);
            return "AI coaching is taking too long to respond right now. Please try again.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenRouter][{RequestId}] Coaching request exception", requestId);
            return "Stay hydrated, get regular exercise, and maintain a balanced diet.";
        }
    }
}

public sealed record OpenRouterChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OpenRouterChatMessage> Messages { get; init; } = [];

    [JsonPropertyName("temperature")]
    public float Temperature { get; init; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; init; }
}

public sealed record OpenRouterChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}

public sealed record OpenRouterChatResponse
{
    [JsonPropertyName("choices")]
    public List<OpenRouterChatChoice>? Choices { get; init; }
}

public sealed record OpenRouterChatChoice
{
    [JsonPropertyName("message")]
    public OpenRouterChatMessage? Message { get; init; }
}
