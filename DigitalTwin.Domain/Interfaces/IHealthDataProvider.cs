using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface IHealthDataProvider
{
    IObservable<VitalSign> GetLiveVitals();

    Task<IEnumerable<VitalSign>> GetLatestSamplesAsync(VitalSignType type, int count = 10);
}
