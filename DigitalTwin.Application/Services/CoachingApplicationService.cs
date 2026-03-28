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
    private readonly ICoachingProvider _coachingProvider;
    private readonly IPatientContextService _patientContextService;
    private readonly ILogger<CoachingApplicationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoachingApplicationService"/> class.
    /// </summary>
    public CoachingApplicationService(
        ICoachingProvider coachingProvider,
        IPatientContextService patientContextService,
        ILogger<CoachingApplicationService> logger)
    {
        _coachingProvider      = coachingProvider;
        _patientContextService = patientContextService;
        _logger                = logger;
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
        var profile = await _patientContextService.BuildContextAsync(ct).ConfigureAwait(false);
        var domain = EnvironmentReadingMapper.ToDomain(environment);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Coaching] Environment advice for location {Location}.", domain.LocationDisplayName);

        var advice = await _coachingProvider.GetEnvironmentAdviceAsync(profile, domain, ct).ConfigureAwait(false);

        return new CoachingAdviceDto
        {
            Advice = advice,
            Timestamp = DateTime.UtcNow
        };
    }
}
