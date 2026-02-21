using System.Reactive.Linq;
using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Mappers;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Services;

namespace DigitalTwin.Application.Services;

public class EnvironmentApplicationService : IEnvironmentApplicationService
{
    private readonly IEnvironmentDataProvider _environmentDataProvider;
    private readonly EnvironmentAssessmentService _assessmentService;

    public EnvironmentApplicationService(
        IEnvironmentDataProvider environmentDataProvider,
        EnvironmentAssessmentService assessmentService)
    {
        _environmentDataProvider = environmentDataProvider;
        _assessmentService = assessmentService;
    }

    public IObservable<RiskEventDto> RiskEvents =>
        _assessmentService.RiskEvents
            .Select(evt => new RiskEventDto
            {
                AirQualityLevel = EnumMapper.ToApp(evt.AirQualityLevel),
                Message = evt.Message,
                Timestamp = evt.Timestamp
            });

    public async Task<EnvironmentReadingDto> GetCurrentEnvironmentAsync()
    {
        var reading = await _environmentDataProvider.GetCurrentAsync();
        var assessed = _assessmentService.AssessReading(reading);
        return EnvironmentReadingMapper.ToDto(assessed);
    }

    public IObservable<EnvironmentReadingDto> SubscribeToEnvironment()
    {
        return _environmentDataProvider.SubscribeToUpdates()
            .Select(reading =>
            {
                var assessed = _assessmentService.AssessReading(reading);
                return EnvironmentReadingMapper.ToDto(assessed);
            });
    }
}
