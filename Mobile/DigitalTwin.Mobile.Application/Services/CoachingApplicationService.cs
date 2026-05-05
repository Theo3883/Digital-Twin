using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

public class CoachingApplicationService
{
    private const string ContractSchemaVersion = "1.0";
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

    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "movement",
        "sleep",
        "nutrition",
        "medication",
        "environment",
        "stress"
    };

    private static readonly string[] DegradedMarkers =
    [
        "stay hydrated, get regular exercise, and maintain a balanced diet",
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
            if (IsDeterministicFallback(_cachedAdvice, environmentContext: false))
            {
                _logger.LogWarning(
                    "[Coaching][{CorrelationId}] Ignoring in-memory deterministic fallback advice cache.",
                    correlationId);
                _cachedAdvice = null;
                _cachedAdviceFingerprint = null;
                _cacheExpiry = DateTime.MinValue;
            }
            else
            {
                _logger.LogInformation("[Coaching][{CorrelationId}] Returning in-memory cached advice.", correlationId);
                return _cachedAdvice;
            }
        }

        try
        {
            var persistentCached = await TryGetCachedAsync(GeneralAdviceCacheKey, fingerprint, CacheTtl, correlationId);
            if (persistentCached != null)
            {
                if (IsDeterministicFallback(persistentCached, environmentContext: false))
                {
                    _logger.LogWarning(
                        "[Coaching][{CorrelationId}] Ignoring persisted deterministic fallback advice cache for key {CacheKey}.",
                        correlationId,
                        GeneralAdviceCacheKey);
                    await TryRemoveCacheKeyAsync(GeneralAdviceCacheKey, correlationId);
                }
                else
                {
                    _cachedAdvice = persistentCached;
                    _cachedAdviceFingerprint = fingerprint;
                    _cacheExpiry = DateTime.UtcNow.Add(CacheTtl);
                    return persistentCached;
                }
            }

            _logger.LogInformation("[Coaching][{CorrelationId}] Generating fresh coaching advice.", correlationId);

            var providerResponse = await _coachingProvider.GetAdviceAsync(context);
            var normalizedAdvice = NormalizeGeneratedAdvice(providerResponse, environmentContext: false, correlationId);
            var isProviderDegraded = IsDegradedAdvice(providerResponse);
            var isDeterministicFallback = IsDeterministicFallback(normalizedAdvice, environmentContext: false);

            if (!isProviderDegraded && !isDeterministicFallback)
            {
                _cachedAdvice = normalizedAdvice;
                _cachedAdviceFingerprint = fingerprint;
                _cacheExpiry = DateTime.UtcNow.Add(CacheTtl);

                await TrySetCachedAsync(GeneralAdviceCacheKey, normalizedAdvice, fingerprint, correlationId);
                await TryCleanupAsync(correlationId);
            }
            else
            {
                _cachedAdvice = null;
                _cachedAdviceFingerprint = null;
                _cacheExpiry = DateTime.MinValue;

                if (isProviderDegraded)
                {
                    _logger.LogWarning(
                        "[Coaching][{CorrelationId}] Skipping coaching cache persistence because provider response is degraded.",
                        correlationId);
                }

                if (isDeterministicFallback)
                {
                    _logger.LogWarning(
                        "[Coaching][{CorrelationId}] Skipping coaching cache persistence because normalized advice is deterministic fallback.",
                        correlationId);
                }
            }

            _logger.LogInformation(
                "[Coaching][{CorrelationId}] Generated normalized advice ({Length} chars, {SectionCount} sections)",
                correlationId,
                normalizedAdvice.Advice.Length,
                normalizedAdvice.Sections.Count);
            return normalizedAdvice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Coaching][{CorrelationId}] Failed to get advice", correlationId);
            return BuildDeterministicFallback(DateTime.UtcNow, environmentContext: false);
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
                if (IsDeterministicFallback(_cachedEnvironmentAdvice, environmentContext: true))
                {
                    _logger.LogWarning(
                        "[Coaching][{CorrelationId}] Ignoring in-memory deterministic fallback environment advice cache.",
                        correlationId);
                    _cachedEnvironmentAdvice = null;
                    _cachedEnvironmentFingerprint = null;
                    _environmentCacheExpiry = DateTime.MinValue;
                }
                else
                {
                    _logger.LogInformation("[Coaching][{CorrelationId}] Returning in-memory cached environment advice.", correlationId);
                    return _cachedEnvironmentAdvice;
                }
            }

            var persistentCached = await TryGetCachedAsync(EnvironmentAdviceCacheKey, fingerprint, EnvironmentCacheTtl, correlationId);
            if (persistentCached != null)
            {
                if (IsDeterministicFallback(persistentCached, environmentContext: true))
                {
                    _logger.LogWarning(
                        "[Coaching][{CorrelationId}] Ignoring persisted deterministic fallback environment advice cache for key {CacheKey}.",
                        correlationId,
                        EnvironmentAdviceCacheKey);
                    await TryRemoveCacheKeyAsync(EnvironmentAdviceCacheKey, correlationId);
                }
                else
                {
                    _cachedEnvironmentAdvice = persistentCached;
                    _cachedEnvironmentFingerprint = fingerprint;
                    _environmentCacheExpiry = DateTime.UtcNow.Add(EnvironmentCacheTtl);
                    return persistentCached;
                }
            }

            var context = $"Environment: AQI={envReading.AqiIndex} ({envReading.AirQuality}), PM2.5={envReading.PM25}, Temperature={envReading.Temperature}°C, Humidity={envReading.Humidity}%";
            if (!string.IsNullOrWhiteSpace(patientContext))
                context += $"\n\nPatient Context:\n{patientContext}";

            var providerResponse = await _coachingProvider.GetAdviceAsync(context);
            var normalizedAdvice = NormalizeGeneratedAdvice(providerResponse, environmentContext: true, correlationId);
            var isProviderDegraded = IsDegradedAdvice(providerResponse);
            var isDeterministicFallback = IsDeterministicFallback(normalizedAdvice, environmentContext: true);

            if (!isProviderDegraded && !isDeterministicFallback)
            {
                _cachedEnvironmentAdvice = normalizedAdvice;
                _cachedEnvironmentFingerprint = fingerprint;
                _environmentCacheExpiry = DateTime.UtcNow.Add(EnvironmentCacheTtl);

                await TrySetCachedAsync(EnvironmentAdviceCacheKey, normalizedAdvice, fingerprint, correlationId);
                await TryCleanupAsync(correlationId);
            }
            else
            {
                _cachedEnvironmentAdvice = null;
                _cachedEnvironmentFingerprint = null;
                _environmentCacheExpiry = DateTime.MinValue;

                if (isProviderDegraded)
                {
                    _logger.LogWarning(
                        "[Coaching][{CorrelationId}] Skipping environment coaching cache persistence because provider response is degraded.",
                        correlationId);
                }

                if (isDeterministicFallback)
                {
                    _logger.LogWarning(
                        "[Coaching][{CorrelationId}] Skipping environment coaching cache persistence because normalized advice is deterministic fallback.",
                        correlationId);
                }
            }

            _logger.LogInformation(
                "[Coaching][{CorrelationId}] Generated normalized environment advice ({Length} chars, {SectionCount} sections)",
                correlationId,
                normalizedAdvice.Advice.Length,
                normalizedAdvice.Sections.Count);
            return normalizedAdvice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Coaching][{CorrelationId}] Failed to get environment advice", correlationId);
            return BuildDeterministicFallback(DateTime.UtcNow, environmentContext: true);
        }
    }

    private CoachingAdviceDto NormalizeGeneratedAdvice(string providerResponse, bool environmentContext, string correlationId)
    {
        var generatedAt = DateTime.UtcNow;

        if (TryParseStructuredAdvice(providerResponse, out var parsed))
        {
            var normalized = NormalizeAdvice(parsed, generatedAt, environmentContext);
            _logger.LogInformation(
                "[Coaching][{CorrelationId}] Structured response accepted ({SectionCount} sections, {ActionCount} actions).",
                correlationId,
                normalized.Sections.Count,
                normalized.Actions.Count);
            return normalized;
        }

        if (TryExtractJsonObject(providerResponse, out var extractedJson) &&
            TryParseStructuredAdvice(extractedJson, out var repaired))
        {
            var normalized = NormalizeAdvice(repaired, generatedAt, environmentContext);
            _logger.LogWarning(
                "[Coaching][{CorrelationId}] Repaired model response by extracting JSON envelope.",
                correlationId);
            return normalized;
        }

        if (!IsDegradedAdvice(providerResponse) &&
            TrySynthesizeLegacyAdvice(providerResponse, environmentContext, generatedAt, out var synthesized))
        {
            _logger.LogWarning(
                "[Coaching][{CorrelationId}] Synthesized structured advice from unstructured payload.",
                correlationId);
            return synthesized;
        }

        _logger.LogWarning(
            "[Coaching][{CorrelationId}] Invalid structured payload. Using deterministic fallback.",
            correlationId);
        return BuildDeterministicFallback(generatedAt, environmentContext);
    }

    private static bool TryParseStructuredAdvice(string raw, out CoachingAdviceDto parsed)
    {
        parsed = null!;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        try
        {
            var value = JsonSerializer.Deserialize(raw, ApplicationJsonContext.Default.CoachingAdviceDto);
            if (value == null)
                return false;

            parsed = value;
            return true;
        }
        catch
        {
            return false;
        }
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

        var start = candidate.IndexOf('{');
        var end = candidate.LastIndexOf('}');
        if (start < 0 || end <= start)
            return false;

        json = candidate[start..(end + 1)].Trim();
        return !string.IsNullOrWhiteSpace(json);
    }

    private static bool TrySynthesizeLegacyAdvice(
        string raw,
        bool environmentContext,
        DateTime generatedAt,
        out CoachingAdviceDto synthesized)
    {
        synthesized = null!;
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

        var lines = candidate
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(CleanText)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
            return false;

        var sections = new List<CoachingSectionDto>();
        var summaryLines = new List<string>();
        var currentTitle = string.Empty;
        var currentItems = new List<string>();

        void FlushSection()
        {
            if (string.IsNullOrWhiteSpace(currentTitle))
                return;

            var cleanedTitle = Clip(CleanText(currentTitle), 50, string.Empty);
            if (string.IsNullOrWhiteSpace(cleanedTitle))
                return;

            var category = NormalizeCategory(cleanedTitle, environmentContext);
            var items = currentItems
                .Select(CleanText)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(item => Clip(item, 90, item))
                .ToList();

            if (items.Count == 0)
                return;

            sections.Add(new CoachingSectionDto
            {
                Category = category,
                Title = cleanedTitle,
                Items = items
            });
        }

        foreach (var line in lines)
        {
            if (TryExtractHeading(line, out var heading))
            {
                FlushSection();
                currentTitle = heading;
                currentItems = new List<string>();
                continue;
            }

            if (TryExtractBullet(line, out var bullet))
            {
                if (string.IsNullOrWhiteSpace(currentTitle))
                    currentTitle = environmentContext ? "Environment" : "Guidance";

                currentItems.Add(Clip(CleanText(bullet), 90, bullet));
                continue;
            }

            if (summaryLines.Count < 3)
                summaryLines.Add(line);
        }

        FlushSection();

        if (sections.Count == 0)
        {
            var looseBullets = lines
                .Select(line => TryExtractBullet(line, out var bullet) ? Clip(CleanText(bullet), 90, bullet) : string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            if (looseBullets.Count > 0)
            {
                var category = environmentContext ? "environment" : "movement";
                sections.Add(new CoachingSectionDto
                {
                    Category = category,
                    Title = CategoryTitle(category),
                    Items = looseBullets
                });
            }
        }

        if (sections.Count == 0 && summaryLines.Count == 0)
            return false;

        var headline = sections.FirstOrDefault()?.Title;
        if (string.IsNullOrWhiteSpace(headline))
            headline = FirstSentence(summaryLines.FirstOrDefault() ?? candidate);

        if (string.IsNullOrWhiteSpace(headline))
            headline = environmentContext ? "Environment guidance" : "Health guidance";

        var summary = summaryLines.Count > 0
            ? string.Join(" ", summaryLines.Take(2))
            : FirstSentence(candidate);

        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = environmentContext
                ? "Based on available data, prioritize lower-pollution periods and steady daily habits."
                : "Based on available data, focus on one or two practical habits today.";
        }

        var actions = new List<CoachingActionDto>();
        var seenActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in sections)
        {
            foreach (var item in section.Items)
            {
                var label = Clip(CleanText(item), 80, string.Empty);
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                var key = $"{section.Category}|{label}";
                if (!seenActions.Add(key))
                    continue;

                actions.Add(new CoachingActionDto
                {
                    Category = section.Category,
                    Label = label
                });

                if (actions.Count == 4)
                    break;
            }

            if (actions.Count == 4)
                break;
        }

        var draft = new CoachingAdviceDto
        {
            SchemaVersion = ContractSchemaVersion,
            Headline = headline,
            Summary = summary,
            Sections = sections,
            Actions = actions,
            Motivation = string.Empty,
            SafetyNote = string.Empty,
            Advice = candidate,
            Timestamp = generatedAt
        };

        synthesized = NormalizeAdvice(draft, generatedAt, environmentContext);
        return true;
    }

    private static bool TryExtractHeading(string line, out string heading)
    {
        heading = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var boldHeading = Regex.Match(line, @"^\*\*(.+?)\*\*$");
        if (boldHeading.Success)
        {
            heading = CleanText(boldHeading.Groups[1].Value);
            return !string.IsNullOrWhiteSpace(heading);
        }

        var markdownHeading = Regex.Match(line, @"^#{1,3}\s+(.+)$");
        if (markdownHeading.Success)
        {
            heading = CleanText(markdownHeading.Groups[1].Value);
            return !string.IsNullOrWhiteSpace(heading);
        }

        var suffixHeading = Regex.Match(line, @"^([A-Za-z][A-Za-z\s]{2,40}):$");
        if (suffixHeading.Success)
        {
            heading = CleanText(suffixHeading.Groups[1].Value);
            return !string.IsNullOrWhiteSpace(heading);
        }

        return false;
    }

    private static bool TryExtractBullet(string line, out string bullet)
    {
        bullet = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var bulletMatch = Regex.Match(line, @"^(?:[-*•]|\d+[\.)])\s+(.+)$");
        if (!bulletMatch.Success)
            return false;

        bullet = CleanText(bulletMatch.Groups[1].Value);
        return !string.IsNullOrWhiteSpace(bullet);
    }

    private static string FirstSentence(string value)
    {
        var cleaned = CleanText(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        var sentenceEnd = cleaned.IndexOfAny(['.', '!', '?']);
        if (sentenceEnd <= 0)
            return cleaned;

        return cleaned[..sentenceEnd].Trim();
    }

    private static CoachingAdviceDto NormalizeAdvice(CoachingAdviceDto value, DateTime generatedAt, bool environmentContext)
    {
        var sections = NormalizeSections(value.Sections, environmentContext);
        var actions = NormalizeActions(value.Actions, sections);

        var headline = Clip(
            CleanText(value.Headline),
            70,
            environmentContext ? "Environment habits for today" : "Health habits for today");

        var summary = Clip(
            EnsureAvailableDataPrefix(CleanText(value.Summary)),
            220,
            "Based on available data, focus on one or two small habits today and build consistency.");

        var motivation = Clip(
            CleanText(value.Motivation),
            120,
            "You are making progress with each small step.");

        var safetyNote = Clip(
            CleanText(value.SafetyNote),
            140,
            "If symptoms worsen, contact your doctor or emergency services.");

        var legacyAdvice = ComposeLegacyAdvice(headline, summary, sections, actions, motivation, safetyNote);

        return new CoachingAdviceDto
        {
            SchemaVersion = ContractSchemaVersion,
            Headline = headline,
            Summary = summary,
            Sections = sections,
            Actions = actions,
            Motivation = motivation,
            SafetyNote = safetyNote,
            Advice = legacyAdvice,
            Timestamp = generatedAt
        };
    }

    private static List<CoachingSectionDto> NormalizeSections(IReadOnlyList<CoachingSectionDto>? sections, bool environmentContext)
    {
        var normalized = new List<CoachingSectionDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (sections != null)
        {
            foreach (var section in sections)
            {
                var category = NormalizeCategory(section.Category, environmentContext);
                var key = category;
                if (!seen.Add(key))
                    continue;

                var title = Clip(CleanText(section.Title), 50, CategoryTitle(category));
                var items = (section.Items ?? [])
                    .Select(CleanText)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .Select(item => Clip(item, 90, item))
                    .ToList();

                if (items.Count < 2)
                    items = DefaultItemsForCategory(category, environmentContext).Take(2).ToList();

                normalized.Add(new CoachingSectionDto
                {
                    Category = category,
                    Title = title,
                    Items = items
                });

                if (normalized.Count == 5)
                    break;
            }
        }

        if (normalized.Count == 0)
            normalized = DefaultSections(environmentContext);

        foreach (var fallback in DefaultSections(environmentContext))
        {
            if (normalized.Count >= 3)
                break;

            if (normalized.Any(existing => existing.Category.Equals(fallback.Category, StringComparison.OrdinalIgnoreCase)))
                continue;

            normalized.Add(fallback);
        }

        return normalized.Take(5).ToList();
    }

    private static List<CoachingActionDto> NormalizeActions(IReadOnlyList<CoachingActionDto>? actions, IReadOnlyList<CoachingSectionDto> sections)
    {
        var normalized = new List<CoachingActionDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (actions != null)
        {
            foreach (var action in actions)
            {
                var category = NormalizeCategory(action.Category, environmentContext: false);
                var label = Clip(CleanText(action.Label), 80, string.Empty);
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                var key = $"{category}|{label}";
                if (!seen.Add(key))
                    continue;

                normalized.Add(new CoachingActionDto
                {
                    Category = category,
                    Label = label
                });

                if (normalized.Count == 6)
                    break;
            }
        }

        if (normalized.Count == 0)
        {
            foreach (var section in sections)
            {
                foreach (var item in section.Items)
                {
                    var label = Clip(CleanText(item), 80, string.Empty);
                    if (string.IsNullOrWhiteSpace(label))
                        continue;

                    var key = $"{section.Category}|{label}";
                    if (!seen.Add(key))
                        continue;

                    normalized.Add(new CoachingActionDto
                    {
                        Category = section.Category,
                        Label = label
                    });

                    if (normalized.Count == 4)
                        break;
                }

                if (normalized.Count == 4)
                    break;
            }
        }

        return normalized.Take(6).ToList();
    }

    private static List<CoachingSectionDto> DefaultSections(bool environmentContext)
    {
        var orderedCategories = environmentContext
            ? new[] { "environment", "movement", "sleep" }
            : new[] { "movement", "sleep", "nutrition" };

        return orderedCategories
            .Select(category => new CoachingSectionDto
            {
                Category = category,
                Title = CategoryTitle(category),
                Items = DefaultItemsForCategory(category, environmentContext).Take(2).ToList()
            })
            .ToList();
    }

    private static IEnumerable<string> DefaultItemsForCategory(string category, bool environmentContext)
    {
        return category switch
        {
            "environment" =>
            [
                "Check AQI before long outdoor sessions.",
                "Prefer low-traffic routes for walking or running."
            ],
            "movement" =>
            [
                "Aim for a 10-20 minute brisk walk today.",
                "Break sitting time with short movement every hour."
            ],
            "sleep" =>
            [
                "Keep a consistent bedtime this week.",
                "Avoid screens for one hour before sleep."
            ],
            "nutrition" =>
            [
                "Build meals around vegetables and lean protein.",
                "Limit ultra-processed snacks today."
            ],
            "medication" =>
            [
                "Take medications exactly as prescribed.",
                "Set reminders to avoid missed doses."
            ],
            "stress" =>
            [
                "Use a 5-minute breathing reset twice today.",
                "Plan one short break to reduce stress load."
            ],
            _ =>
            [
                environmentContext
                    ? "Based on available data, stay consistent with low-risk healthy habits today."
                    : "Based on available data, focus on one simple healthy habit today.",
                "Track your progress and adjust gradually."
            ]
        };
    }

    private static CoachingAdviceDto BuildDeterministicFallback(DateTime generatedAt, bool environmentContext)
    {
        var sections = DefaultSections(environmentContext);
        var actions = sections
            .SelectMany(section => section.Items.Take(1).Select(item => new CoachingActionDto
            {
                Category = section.Category,
                Label = Clip(item, 80, item)
            }))
            .ToList();

        var headline = environmentContext ? "Environment-safe routine" : "Steady habit routine";
        var summary = environmentContext
            ? "Based on available data, choose low-pollution windows, move gently, and keep your recovery routine consistent."
            : "Based on available data, choose one movement habit, one sleep habit, and one nutrition habit for today.";
        var motivation = "Consistency beats intensity. Small steps today create strong results.";
        var safetyNote = "If symptoms worsen, contact your doctor or emergency services.";

        return new CoachingAdviceDto
        {
            SchemaVersion = ContractSchemaVersion,
            Headline = headline,
            Summary = summary,
            Sections = sections,
            Actions = actions,
            Motivation = motivation,
            SafetyNote = safetyNote,
            Advice = ComposeLegacyAdvice(headline, summary, sections, actions, motivation, safetyNote),
            Timestamp = generatedAt
        };
    }

    private static bool IsDeterministicFallback(CoachingAdviceDto advice, bool environmentContext)
    {
        var expected = BuildDeterministicFallback(advice.Timestamp, environmentContext);

        if (!string.Equals(CleanText(advice.Headline), expected.Headline, StringComparison.Ordinal))
            return false;

        if (!string.Equals(CleanText(advice.Summary), expected.Summary, StringComparison.Ordinal))
            return false;

        if (!string.Equals(CleanText(advice.Motivation), expected.Motivation, StringComparison.Ordinal))
            return false;

        if (!string.Equals(CleanText(advice.SafetyNote), expected.SafetyNote, StringComparison.Ordinal))
            return false;

        return AreSectionsEquivalent(advice.Sections, expected.Sections)
            && AreActionsEquivalent(advice.Actions, expected.Actions);
    }

    private static bool AreSectionsEquivalent(IReadOnlyList<CoachingSectionDto> left, IReadOnlyList<CoachingSectionDto> right)
    {
        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i].Category, right[i].Category, StringComparison.Ordinal))
                return false;

            if (!string.Equals(left[i].Title, right[i].Title, StringComparison.Ordinal))
                return false;

            if (left[i].Items.Count != right[i].Items.Count)
                return false;

            for (var j = 0; j < left[i].Items.Count; j++)
            {
                if (!string.Equals(left[i].Items[j], right[i].Items[j], StringComparison.Ordinal))
                    return false;
            }
        }

        return true;
    }

    private static bool AreActionsEquivalent(IReadOnlyList<CoachingActionDto> left, IReadOnlyList<CoachingActionDto> right)
    {
        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i].Category, right[i].Category, StringComparison.Ordinal))
                return false;

            if (!string.Equals(left[i].Label, right[i].Label, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static string ComposeLegacyAdvice(
        string headline,
        string summary,
        IReadOnlyList<CoachingSectionDto> sections,
        IReadOnlyList<CoachingActionDto> actions,
        string motivation,
        string safetyNote)
    {
        var builder = new StringBuilder();
        builder.Append("**").Append(headline).AppendLine("**");
        builder.AppendLine(summary);

        foreach (var section in sections)
        {
            builder.AppendLine();
            builder.Append("**").Append(section.Title).AppendLine("**");
            foreach (var item in section.Items)
            {
                builder.Append("• ").AppendLine(item);
            }
        }

        if (actions.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("**Actions**");
            foreach (var action in actions)
            {
                builder.Append("• ").AppendLine(action.Label);
            }
        }

        builder.AppendLine();
        builder.Append('*').Append(motivation).AppendLine("*");
        builder.AppendLine(safetyNote);

        return builder.ToString().Trim();
    }

    private static string NormalizeCategory(string? value, bool environmentContext)
    {
        var normalized = CleanText(value).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            return environmentContext ? "environment" : "movement";

        normalized = normalized switch
        {
            "exercise" or "activity" or "fitness" => "movement",
            "air" or "air quality" => "environment",
            "food" or "diet" => "nutrition",
            "mental" => "stress",
            _ => normalized
        };

        if (!AllowedCategories.Contains(normalized))
            return environmentContext ? "environment" : "movement";

        return normalized;
    }

    private static string CategoryTitle(string category)
    {
        return category switch
        {
            "movement" => "Movement",
            "sleep" => "Sleep",
            "nutrition" => "Nutrition",
            "medication" => "Medication",
            "environment" => "Environment",
            "stress" => "Stress",
            _ => "Guidance"
        };
    }

    private static string EnsureAvailableDataPrefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Based on available data, focus on one or two practical habits today.";

        return value.StartsWith("Based on available data,", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"Based on available data, {value}";
    }

    private static string CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\t", " ", StringComparison.Ordinal)
            .Trim();
    }

    private static string Clip(string value, int maxLength, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (source.Length <= maxLength)
            return source;

        return new string(source.AsSpan(0, maxLength)).TrimEnd();
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

    private async Task TryRemoveCacheKeyAsync(string cacheKey, string correlationId)
    {
        try
        {
            await _cacheStore.RemoveAsync(cacheKey);
            _logger.LogInformation("[Coaching][{CorrelationId}] Removed cache key {CacheKey}.", correlationId, cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Coaching][{CorrelationId}] Failed to remove cache key {CacheKey}.", correlationId, cacheKey);
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
