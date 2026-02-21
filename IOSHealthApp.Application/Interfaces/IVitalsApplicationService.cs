using IOSHealthApp.Application.DTOs;
using IOSHealthApp.Domain.Enums;

namespace IOSHealthApp.Application.Interfaces;

public interface IVitalsApplicationService
{
    IObservable<VitalSignDto> GetLiveVitals();

    Task<IEnumerable<VitalSignDto>> GetLatestSamplesAsync(VitalSignType type, int count = 20);
}
