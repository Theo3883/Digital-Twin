using DigitalTwin.Mobile.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Infrastructure.Services;

internal static class AiProviderResponseClassifier
{
    private const string GenericCoachingFallback = "stay hydrated, get regular exercise, and maintain a balanced diet.";

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

        try
        {
            primaryResponse = await _primary.GetAdviceAsync(patientContext, ct);
            if (!AiProviderResponseClassifier.IsDegraded(primaryResponse))
                return primaryResponse;

            _logger.LogWarning(
                "[AI-Fallback][{RequestId}] Primary coaching provider returned degraded response. Trying OpenRouter fallback.",
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
                "[AI-Fallback][{RequestId}] Primary coaching provider failed. Trying OpenRouter fallback.",
                requestId);
        }

        try
        {
            var fallbackResponse = await _fallback.GetAdviceAsync(patientContext, ct);
            if (!AiProviderResponseClassifier.IsDegraded(fallbackResponse))
            {
                _logger.LogInformation("[AI-Fallback][{RequestId}] OpenRouter coaching fallback succeeded.", requestId);
                return fallbackResponse;
            }

            _logger.LogWarning(
                "[AI-Fallback][{RequestId}] OpenRouter coaching fallback also returned degraded response.",
                requestId);

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
