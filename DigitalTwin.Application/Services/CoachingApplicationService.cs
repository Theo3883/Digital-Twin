using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Thin orchestrator: delegates context building to the Domain service,
/// delegates AI call to <see cref="ICoachingProvider"/>, maps to DTO.
/// </summary>
public class CoachingApplicationService : ICoachingApplicationService
{
    private readonly ICoachingProvider _coachingProvider;
    private readonly IPatientContextService _patientContextService;
    private readonly ILogger<CoachingApplicationService> _logger;

    public CoachingApplicationService(
        ICoachingProvider coachingProvider,
        IPatientContextService patientContextService,
        ILogger<CoachingApplicationService> logger)
    {
        _coachingProvider      = coachingProvider;
        _patientContextService = patientContextService;
        _logger                = logger;
    }

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

        _logger.LogInformation("[Coaching] Requesting advice for patient {PatientId}.", profile.Id);

        var advice = await _coachingProvider.GetAdviceAsync(profile);

        return new CoachingAdviceDto
        {
            Advice    = advice,
            Timestamp = DateTime.UtcNow
        };
    }
}
