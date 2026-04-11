using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

public class CoachingApplicationService
{
    private const string GeneralAdviceCacheKey = "coaching:general:v1";
    private const string EnvironmentAdviceCacheKey = "coaching:environment:v1";

    private readonly ICoachingProvider _coachingProvider;
    private readonly PatientAiContextBuilder _contextBuilder;
    private readonly ITypedCacheStore _cacheStore;
    private readonly ILogger<CoachingApplicationService> _logger;

    private CoachingAdviceDto? _cachedAdvice;
    private string? _cachedAdviceFingerprint;
    private DateTime _cacheExpiry = DateTime.MinValue;

    private CoachingAdviceDto? _cachedEnvironmentAdvice;
    private string? _cachedEnvironmentFingerprint;
    private DateTime _environmentCacheExpiry = DateTime.MinValue;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(4);
    private static readonly TimeSpan EnvironmentCacheTtl = TimeSpan.FromHours(2);
    private static readonly TimeSpan CacheCleanupHorizon = TimeSpan.FromDays(3);

    private static readonly string[] DegradedMarkers =
    [
        "temporarily rate limited",
        "quota is exhausted",
        "taking too long to respond",
        "trouble connecting",
        "error occurred",
        "not configured"
    ];

    public CoachingApplicationService(
        ICoachingProvider coachingProvider,
        PatientAiContextBuilder contextBuilder,
        ITypedCacheStore cacheStore,
        ILogger<CoachingApplicationService> logger)
    {
        _coachingProvider = coachingProvider;
        _contextBuilder = contextBuilder;
        _cacheStore = cacheStore;
        _logger = logger;
    }

    public async Task<CoachingAdviceDto> GetAdviceAsync()
    {
        var correlationId = ResolveCorrelationId();
        var context = await _contextBuilder.BuildCoachingContextAsync();
        var fingerprint = ComputeFingerprint(context);

        if (_cachedAdvice != null &&
            DateTime.UtcNow < _cacheExpiry &&
            string.Equals(_cachedAdviceFingerprint, fingerprint, StringComparison.Ordinal))
        {
            _logger.LogInformation("[Coaching][{CorrelationId}] Returning in-memory cached advice.", correlationId);
            return _cachedAdvice;
        }

        try
        {
            var persistentCached = await TryGetCachedAsync(GeneralAdviceCacheKey, fingerprint, CacheTtl, correlationId);
            if (persistentCached != null)
            {
                _cachedAdvice = persistentCached;
                _cachedAdviceFingerprint = fingerprint;
                _cacheExpiry = DateTime.UtcNow.Add(CacheTtl);
                return persistentCached;
            }

            _logger.LogInformation("[Coaching][{CorrelationId}] Generating fresh coaching advice.", correlationId);

            var advice = await _coachingProvider.GetAdviceAsync(context);

            _cachedAdvice = new CoachingAdviceDto
            {
                Advice = advice,
                Timestamp = DateTime.UtcNow
            };
            _cachedAdviceFingerprint = fingerprint;
            _cacheExpiry = DateTime.UtcNow.Add(CacheTtl);

            if (!IsDegradedAdvice(advice))
            {
                await TrySetCachedAsync(GeneralAdviceCacheKey, _cachedAdvice, fingerprint, correlationId);
                await TryCleanupAsync(correlationId);
            }
            else
            {
                _logger.LogWarning("[Coaching][{CorrelationId}] Skipping persistent cache for degraded coaching advice.", correlationId);
            }

            _logger.LogInformation("[Coaching][{CorrelationId}] Generated advice ({Length} chars)", correlationId, advice.Length);
            return _cachedAdvice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Coaching][{CorrelationId}] Failed to get advice", correlationId);
            return new CoachingAdviceDto
            {
                Advice = "Stay hydrated, get regular exercise, and maintain a balanced diet.",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    public async Task<CoachingAdviceDto> GetEnvironmentAdviceAsync(EnvironmentReadingDto? envReading)
    {
        var correlationId = ResolveCorrelationId();

        try
        {
            if (envReading == null)
            {
                _logger.LogInformation("[Coaching][{CorrelationId}] Environment advice skipped (no environment data).", correlationId);
                return new CoachingAdviceDto { Advice = "No environment data available.", Timestamp = DateTime.UtcNow };
            }

            var patientContext = await _contextBuilder.BuildCoachingContextAsync();
            var fingerprint = BuildEnvironmentFingerprint(envReading, patientContext);

            if (_cachedEnvironmentAdvice != null &&
                DateTime.UtcNow < _environmentCacheExpiry &&
                string.Equals(_cachedEnvironmentFingerprint, fingerprint, StringComparison.Ordinal))
            {
                _logger.LogInformation("[Coaching][{CorrelationId}] Returning in-memory cached environment advice.", correlationId);
                return _cachedEnvironmentAdvice;
            }

            var persistentCached = await TryGetCachedAsync(EnvironmentAdviceCacheKey, fingerprint, EnvironmentCacheTtl, correlationId);
            if (persistentCached != null)
            {
                _cachedEnvironmentAdvice = persistentCached;
                _cachedEnvironmentFingerprint = fingerprint;
                _environmentCacheExpiry = DateTime.UtcNow.Add(EnvironmentCacheTtl);
                return persistentCached;
            }

            var context = $"Environment: AQI={envReading.AqiIndex} ({envReading.AirQuality}), PM2.5={envReading.PM25}, Temperature={envReading.Temperature}°C, Humidity={envReading.Humidity}%";
            if (!string.IsNullOrWhiteSpace(patientContext))
                context += $"\n\nPatient Context:\n{patientContext}";

            var advice = await _coachingProvider.GetAdviceAsync(context);

            _cachedEnvironmentAdvice = new CoachingAdviceDto
            {
                Advice = advice,
                Timestamp = DateTime.UtcNow
            };
            _cachedEnvironmentFingerprint = fingerprint;
            _environmentCacheExpiry = DateTime.UtcNow.Add(EnvironmentCacheTtl);

            if (!IsDegradedAdvice(advice))
            {
                await TrySetCachedAsync(EnvironmentAdviceCacheKey, _cachedEnvironmentAdvice, fingerprint, correlationId);
                await TryCleanupAsync(correlationId);
            }
            else
            {
                _logger.LogWarning("[Coaching][{CorrelationId}] Skipping persistent cache for degraded environment advice.", correlationId);
            }

            _logger.LogInformation("[Coaching][{CorrelationId}] Generated environment advice ({Length} chars)", correlationId, advice.Length);
            return _cachedEnvironmentAdvice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Coaching][{CorrelationId}] Failed to get environment advice", correlationId);
            return new CoachingAdviceDto
            {
                Advice = "Monitor air quality and limit outdoor activity when AQI is high.",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private static string ResolveCorrelationId()
    {
        var traceId = Activity.Current?.TraceId.ToString();
        return string.IsNullOrWhiteSpace(traceId)
            ? Guid.NewGuid().ToString("N")[..8]
            : traceId;
    }

    private async Task<CoachingAdviceDto?> TryGetCachedAsync(
        string cacheKey,
        string fingerprint,
        TimeSpan ttl,
        string correlationId)
    {
        try
        {
            var entry = await _cacheStore.GetAsync(cacheKey, ApplicationJsonContext.Default.CoachingAdviceDto);
            if (entry == null)
                return null;

            if (!string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal))
                return null;

            if (DateTime.UtcNow - entry.StoredAtUtc > ttl)
                return null;

            _logger.LogInformation("[Coaching][{CorrelationId}] Returning persistent cached advice for key {CacheKey}.", correlationId, cacheKey);
            return entry.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Coaching][{CorrelationId}] Failed to read cache key {CacheKey}. Continuing without cache.", correlationId, cacheKey);
            return null;
        }
    }

    private async Task TrySetCachedAsync(string cacheKey, CoachingAdviceDto advice, string fingerprint, string correlationId)
    {
        try
        {
            await _cacheStore.SetAsync(
                cacheKey,
                new CacheEnvelope<CoachingAdviceDto>
                {
                    Value = advice,
                    StoredAtUtc = DateTime.UtcNow,
                    Fingerprint = fingerprint
                },
                ApplicationJsonContext.Default.CoachingAdviceDto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Coaching][{CorrelationId}] Failed to persist cache key {CacheKey}.", correlationId, cacheKey);
        }
    }

    private async Task TryCleanupAsync(string correlationId)
    {
        try
        {
            await _cacheStore.DeleteOlderThanAsync(DateTime.UtcNow.Subtract(CacheCleanupHorizon));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[Coaching][{CorrelationId}] Cache cleanup skipped due to non-fatal error.", correlationId);
        }
    }

    private static string BuildEnvironmentFingerprint(EnvironmentReadingDto envReading, string patientContext)
    {
        var normalized = FormattableString.Invariant(
            $"{envReading.Timestamp:O}|{envReading.AqiIndex}|{envReading.PM25:F3}|{envReading.Temperature:F2}|{envReading.Humidity:F2}|{patientContext}");
        return ComputeFingerprint(normalized);
    }

    private static string ComputeFingerprint(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool IsDegradedAdvice(string advice)
    {
        if (string.IsNullOrWhiteSpace(advice))
            return true;

        var normalized = advice.ToLowerInvariant();
        return DegradedMarkers.Any(normalized.Contains);
    }
}
