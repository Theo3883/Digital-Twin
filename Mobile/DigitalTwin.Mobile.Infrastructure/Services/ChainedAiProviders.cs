using System.Text.Json;
using DigitalTwin.Mobile.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Infrastructure.Services;

internal static class AiProviderResponseClassifier
{
    private const string GenericCoachingFallback = "stay hydrated, get regular exercise, and maintain a balanced diet.";
    private static readonly string[] RequiredCoachingKeys =
    [
        "schemaVersion",
        "headline",
        "summary",
        "sections",
        "actions",
        "motivation",
        "safetyNote"
    ];

    private static readonly string[] DegradedMarkers =
    [
        "not configured",
        "temporarily rate limited",
        "quota is exhausted",
        "trouble connecting",
        "error occurred",
        "i wasn't able to generate a response",
        "please set up your"
    ];

    internal static bool IsDegraded(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return true;

        var normalized = response.Trim().ToLowerInvariant();
        if (normalized == GenericCoachingFallback)
            return true;

        return DegradedMarkers.Any(normalized.Contains);
    }

    internal static bool IsStructuredCoachingPayload(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;

        if (!TryExtractJsonObject(response, out var jsonCandidate))
            return false;

        try
        {
            using var document = JsonDocument.Parse(jsonCandidate);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var root = document.RootElement;
            foreach (var key in RequiredCoachingKeys)
            {
                if (!TryGetPropertyIgnoreCase(root, key, out _))
                    return false;
            }

            if (!TryGetPropertyIgnoreCase(root, "sections", out var sections) ||
                sections.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            if (!TryGetPropertyIgnoreCase(root, "actions", out var actions) ||
                actions.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement root, string propertyName, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryExtractJsonObject(string raw, out string json)
    {
        json = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var candidate = raw.Trim();
        if (candidate.StartsWith("```", StringComparison.Ordinal))
        {
            candidate = candidate
                .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("```", string.Empty, StringComparison.Ordinal)
                .Trim();
        }

        if (candidate.StartsWith("{", StringComparison.Ordinal) &&
            candidate.EndsWith("}", StringComparison.Ordinal))
        {
            json = candidate;
            return true;
        }

        var start = candidate.IndexOf('{');
        var end = candidate.LastIndexOf('}');
        if (start < 0 || end <= start)
            return false;

        json = candidate[start..(end + 1)].Trim();
        return !string.IsNullOrWhiteSpace(json);
    }
}

public sealed class ChainedChatBotProvider : IChatBotProvider
{
    private readonly IChatBotProvider _primary;
    private readonly IChatBotProvider _fallback;
    private readonly ILogger<ChainedChatBotProvider> _logger;

    public ChainedChatBotProvider(IChatBotProvider primary, IChatBotProvider fallback, ILogger<ChainedChatBotProvider> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<string> SendMessageAsync(string userMessage, string? patientContext, CancellationToken ct = default)
    {
        var requestId = GeminiRetryPolicy.ResolveCorrelationId();
        string? primaryResponse = null;

        try
        {
            primaryResponse = await _primary.SendMessageAsync(userMessage, patientContext, ct);
            if (!AiProviderResponseClassifier.IsDegraded(primaryResponse))
                return primaryResponse;

            _logger.LogWarning(
                "[AI-Fallback][{RequestId}] Primary chat provider returned degraded response. Trying OpenRouter fallback.",
                requestId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[AI-Fallback][{RequestId}] Primary chat provider failed. Trying OpenRouter fallback.",
                requestId);
        }

        try
        {
            var fallbackResponse = await _fallback.SendMessageAsync(userMessage, patientContext, ct);
            if (!AiProviderResponseClassifier.IsDegraded(fallbackResponse))
            {
                _logger.LogInformation("[AI-Fallback][{RequestId}] OpenRouter chat fallback succeeded.", requestId);
                return fallbackResponse;
            }

            _logger.LogWarning(
                "[AI-Fallback][{RequestId}] OpenRouter chat fallback also returned degraded response.",
                requestId);

            return string.IsNullOrWhiteSpace(primaryResponse) ? fallbackResponse : primaryResponse;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[AI-Fallback][{RequestId}] OpenRouter chat fallback failed.",
                requestId);

            return primaryResponse ?? "I'm having trouble connecting to the AI service right now.";
        }
    }
}

public sealed class ChainedCoachingProvider : ICoachingProvider
{
    private readonly ICoachingProvider _primary;
    private readonly ICoachingProvider _fallback;
    private readonly ILogger<ChainedCoachingProvider> _logger;

    public ChainedCoachingProvider(ICoachingProvider primary, ICoachingProvider fallback, ILogger<ChainedCoachingProvider> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<string> GetAdviceAsync(string patientContext, CancellationToken ct = default)
    {
        var requestId = GeminiRetryPolicy.ResolveCorrelationId();
        string? primaryResponse = null;
        var primaryStructured = false;

        try
        {
            primaryResponse = await _primary.GetAdviceAsync(patientContext, ct);
            primaryStructured = AiProviderResponseClassifier.IsStructuredCoachingPayload(primaryResponse);

            if (!AiProviderResponseClassifier.IsDegraded(primaryResponse) && primaryStructured)
                return primaryResponse;

            if (!AiProviderResponseClassifier.IsDegraded(primaryResponse) && !primaryStructured)
            {
                _logger.LogWarning(
                    "[AI-Fallback][{RequestId}] Primary coaching provider returned unstructured payload. Trying OpenRouter fallback.",
                    requestId);
            }
            else
            {
                _logger.LogWarning(
                    "[AI-Fallback][{RequestId}] Primary coaching provider returned degraded response. Trying OpenRouter fallback.",
                    requestId);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[AI-Fallback][{RequestId}] Primary coaching provider failed. Trying OpenRouter fallback.",
                requestId);
        }

        try
        {
            var fallbackResponse = await _fallback.GetAdviceAsync(patientContext, ct);
            var fallbackStructured = AiProviderResponseClassifier.IsStructuredCoachingPayload(fallbackResponse);

            if (!AiProviderResponseClassifier.IsDegraded(fallbackResponse) && fallbackStructured)
            {
                _logger.LogInformation("[AI-Fallback][{RequestId}] OpenRouter coaching fallback succeeded.", requestId);
                return fallbackResponse;
            }

            if (!AiProviderResponseClassifier.IsDegraded(fallbackResponse) && !fallbackStructured)
            {
                _logger.LogWarning(
                    "[AI-Fallback][{RequestId}] OpenRouter coaching fallback returned unstructured payload. Passing through to application normalizer.",
                    requestId);
                return fallbackResponse;
            }
            else
            {
                _logger.LogWarning(
                    "[AI-Fallback][{RequestId}] OpenRouter coaching fallback also returned degraded response.",
                    requestId);
            }

            if (primaryStructured && !string.IsNullOrWhiteSpace(primaryResponse))
                return primaryResponse;

            return string.IsNullOrWhiteSpace(primaryResponse)
                ? "Stay hydrated, get regular exercise, and maintain a balanced diet."
                : primaryResponse;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[AI-Fallback][{RequestId}] OpenRouter coaching fallback failed.",
                requestId);

            return primaryResponse ?? "Stay hydrated, get regular exercise, and maintain a balanced diet.";
        }
    }
}
