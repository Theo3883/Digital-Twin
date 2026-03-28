using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Services;

/// <summary>
/// Computes 24h environment vs heart rate analytics from persisted readings and vitals.
/// </summary>
public interface IEnvironmentHealthAnalyticsService
{
    /// <param name="patientId">When null, heart rate series is empty (no patient context).</param>
    Task<EnvironmentAnalyticsResult> ComputeLast24HoursAsync(
        Guid? patientId,
        CancellationToken cancellationToken = default);
}
