using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Mappers;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Orchestrates personalized coaching advice generation.
/// </summary>
public class CoachingApplicationService : ICoachingApplicationService
{
    private static readonly TimeSpan DefaultAdviceTtl = TimeSpan.FromHours(4);

    private readonly ICoachingProvider _coachingProvider;
    private readonly IPatientContextService _patientContextService;
    private readonly IEnvironmentReadingCache? _envCache;
    private readonly ILogger<CoachingApplicationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoachingApplicationService"/> class.
    /// </summary>
    public CoachingApplicationService(
        ICoachingProvider coachingProvider,
        IPatientContextService patientContextService,
        ILogger<CoachingApplicationService> logger,
        IEnvironmentReadingCache? envCache = null)
    {
        _coachingProvider      = coachingProvider;
        _patientContextService = patientContextService;
        _logger                = logger;
        _envCache              = envCache;
    }

    /// <summary>
    /// Generates coaching advice using the current patient context when available.
    /// </summary>
    public async Task<CoachingAdviceDto> GetAdviceAsync(CancellationToken ct = default)
    {
        var profile = await _patientContextService.BuildContextAsync(ct);

        if (profile is null)
        {
            return new CoachingAdviceDto
            {
                Advice    = "Sign in to receive personalized coaching advice.",
                Timestamp = DateTime.UtcNow
            };
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Coaching] Requesting advice for patient {PatientId}.", profile.Id);

        var advice = await _coachingProvider.GetAdviceAsync(profile);

        return new CoachingAdviceDto
        {
            Advice    = advice,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <inheritdoc />
    public async Task<CoachingAdviceDto> GetEnvironmentAdviceAsync(EnvironmentReadingDto environment, CancellationToken ct = default)
    {
        // Return cached advice if it is still fresh — avoids a Gemini call on every home-page load.
        var cached = _envCache?.GetLastAdvice();
        if (cached is not null && (DateTime.UtcNow - cached.Timestamp) < DefaultAdviceTtl)
        {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[Coaching] Returning cached environment advice (age: {Age:mm\\:ss}).",
                    DateTime.UtcNow - cached.Timestamp);
            return cached;
        }

        var profile = await _patientContextService.BuildContextAsync(ct).ConfigureAwait(false);
        var domain = EnvironmentReadingMapper.ToDomain(environment);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Coaching] Environment advice for location {Location}.", domain.LocationDisplayName);

        var advice = await _coachingProvider.GetEnvironmentAdviceAsync(profile, domain, ct).ConfigureAwait(false);

        var dto = new CoachingAdviceDto
        {
            Advice = advice,
            Timestamp = DateTime.UtcNow
        };

        _envCache?.SaveAdvice(dto);

        return dto;
    }

    /// <inheritdoc />
    public CoachingAdviceDto? GetLastEnvironmentAdvice() => _envCache?.GetLastAdvice();

    /// <inheritdoc />
    public bool IsEnvironmentAdviceFresh(TimeSpan? maxAge = null)
    {
        var cached = _envCache?.GetLastAdvice();
        if (cached is null) return false;
        return (DateTime.UtcNow - cached.Timestamp) < (maxAge ?? DefaultAdviceTtl);
    }

    /// <inheritdoc />
    public void ClearEnvironmentAdviceCache() => _envCache?.ClearAdvice();
}
