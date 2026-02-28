using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Providers;

public interface IHealthDataProvider
{
    IObservable<VitalSign> GetLiveVitals();

    Task<IEnumerable<VitalSign>> GetLatestSamplesAsync(VitalSignType type, int count = 10);

    /// <summary>
    /// Queries sleep sessions within the specified date range.
    /// </summary>
    Task<IEnumerable<SleepSession>> GetSleepSessionsAsync(DateTime from, DateTime to);

    /// <summary>
    /// Requests read permissions for all required health data types.
    /// Returns true if permissions were granted.
    /// </summary>
    Task<bool> RequestPermissionsAsync();
}
