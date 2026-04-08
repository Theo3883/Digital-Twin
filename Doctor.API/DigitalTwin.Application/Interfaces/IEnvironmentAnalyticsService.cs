using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Builds last-24h environment vs heart-rate analytics for charts.
/// </summary>
public interface IEnvironmentAnalyticsService
{
    Task<EnvironmentAnalyticsDto> GetLast24HoursAsync(CancellationToken cancellationToken = default);
}
