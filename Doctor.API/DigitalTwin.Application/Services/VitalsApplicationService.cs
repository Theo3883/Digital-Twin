using System.Reactive.Linq;
using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Enums;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Mappers;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Provides live and recent vital-sign data for application consumers.
/// </summary>
public class VitalsApplicationService : IVitalsApplicationService
{
    private readonly IHealthDataProvider _healthDataProvider;
    private readonly IVitalSignService _vitalSignService;
    private readonly Dictionary<Domain.Enums.VitalSignType, double> _lastValues = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="VitalsApplicationService"/> class.
    /// </summary>
    public VitalsApplicationService(
        IHealthDataProvider healthDataProvider,
        IVitalSignService vitalSignService)
    {
        _healthDataProvider = healthDataProvider;
        _vitalSignService = vitalSignService;
    }

    /// <summary>
    /// Gets the live vital-sign stream with per-type trend values applied.
    /// </summary>
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

    /// <summary>
    /// Gets the latest stored samples for the requested vital-sign type.
    /// </summary>
    public async Task<IEnumerable<VitalSignDto>> GetLatestSamplesAsync(VitalSignType type, int count = 20)
    {
        var domainType = EnumMapper.ToDomain(type);
        var samples = await _healthDataProvider.GetLatestSamplesAsync(domainType, count);
        return samples.Select(s => VitalSignMapper.ToDto(s));
    }
}
