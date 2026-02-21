using IOSHealthApp.Domain.Enums;
using IOSHealthApp.Domain.Models;

namespace IOSHealthApp.Domain.Interfaces;

public interface IHealthDataProvider
{
    IObservable<VitalSign> GetLiveVitals();

    Task<IEnumerable<VitalSign>> GetLatestSamplesAsync(VitalSignType type, int count = 10);
}
