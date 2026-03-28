using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Interfaces.Services;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Orchestrates environment analytics: resolves current patient and maps domain results to DTOs.
/// </summary>
public sealed class EnvironmentAnalyticsService : IEnvironmentAnalyticsService
{
    private readonly IEnvironmentHealthAnalyticsService _analytics;
    private readonly IAuthApplicationService _auth;

    public EnvironmentAnalyticsService(
        IEnvironmentHealthAnalyticsService analytics,
        IAuthApplicationService auth)
    {
        _analytics = analytics;
        _auth = auth;
    }

    public async Task<EnvironmentAnalyticsDto> GetLast24HoursAsync(CancellationToken cancellationToken = default)
    {
        var patientId = await _auth.GetCurrentPatientIdAsync().ConfigureAwait(false);
        var result = await _analytics.ComputeLast24HoursAsync(patientId, cancellationToken).ConfigureAwait(false);

        return new EnvironmentAnalyticsDto
        {
            HeartRatePath = result.HeartRatePath,
            Pm25Path = result.Pm25Path,
            CorrelationR = result.CorrelationR,
            Footnote = result.Footnote,
            HasPm25Series = result.HasPm25Series,
            HasHeartRateSeries = result.HasHeartRateSeries
        };
    }
}
