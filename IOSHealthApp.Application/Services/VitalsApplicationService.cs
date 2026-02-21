using System.Reactive.Linq;
using IOSHealthApp.Application.DTOs;
using IOSHealthApp.Application.Interfaces;
using IOSHealthApp.Application.Mappers;
using IOSHealthApp.Domain.Enums;
using IOSHealthApp.Domain.Interfaces;
using IOSHealthApp.Domain.Services;

namespace IOSHealthApp.Application.Services;

public class VitalsApplicationService : IVitalsApplicationService
{
    private readonly IHealthDataProvider _healthDataProvider;
    private readonly VitalSignService _vitalSignService;
    private readonly Dictionary<VitalSignType, double> _lastValues = new();

    public VitalsApplicationService(
        IHealthDataProvider healthDataProvider,
        VitalSignService vitalSignService)
    {
        _healthDataProvider = healthDataProvider;
        _vitalSignService = vitalSignService;
    }

    public IObservable<VitalSignDto> GetLiveVitals()
    {
        return _healthDataProvider.GetLiveVitals()
            .Select(vital =>
            {
                var trend = 0;
                if (_lastValues.TryGetValue(vital.Type, out var previous))
                {
                    trend = _vitalSignService.ComputeTrend(vital.Value, previous);
                }
                _lastValues[vital.Type] = vital.Value;

                return VitalSignMapper.ToDto(vital, trend);
            });
    }

    public async Task<IEnumerable<VitalSignDto>> GetLatestSamplesAsync(VitalSignType type, int count = 20)
    {
        var samples = await _healthDataProvider.GetLatestSamplesAsync(type, count);
        return samples.Select(s => VitalSignMapper.ToDto(s));
    }
}
