using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Providers;

public interface ICoachingProvider
{
    Task<string> GetAdviceAsync(PatientProfile profile);

    /// <summary>
    /// Short environment-aware guidance (e.g. air quality + activity). Profile may be null if unavailable.
    /// </summary>
    Task<string> GetEnvironmentAdviceAsync(PatientProfile? profile, EnvironmentReading environment, CancellationToken cancellationToken = default);
}
