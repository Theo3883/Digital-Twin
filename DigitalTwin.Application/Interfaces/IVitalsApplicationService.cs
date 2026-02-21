using DigitalTwin.Application.DTOs;
using DigitalTwin.Domain.Enums;

namespace DigitalTwin.Application.Interfaces;

public interface IVitalsApplicationService
{
    IObservable<VitalSignDto> GetLiveVitals();

    Task<IEnumerable<VitalSignDto>> GetLatestSamplesAsync(VitalSignType type, int count = 20);
}
