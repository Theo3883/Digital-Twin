using System.Reactive.Linq;
using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Mappers;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Thin orchestrator for environment data.
/// Persistence strategy (cloud-first / local-fallback) is delegated to
/// <see cref="IPersistenceGateway{EnvironmentReading}"/> — no repository
/// interfaces are injected here.
/// </summary>
public class EnvironmentApplicationService : IEnvironmentApplicationService
{
    private readonly IEnvironmentDataProvider                         _environmentDataProvider;
    private readonly IEnvironmentAssessmentService                    _assessmentService;
    private readonly IPersistenceGateway<Domain.Models.EnvironmentReading> _gateway;
    private readonly ILogger<EnvironmentApplicationService>           _logger;

    public EnvironmentApplicationService(
        IEnvironmentDataProvider environmentDataProvider,
        IEnvironmentAssessmentService assessmentService,
        IPersistenceGateway<Domain.Models.EnvironmentReading> gateway,
        ILogger<EnvironmentApplicationService> logger)
    {
        _environmentDataProvider = environmentDataProvider;
        _assessmentService       = assessmentService;
        _gateway                 = gateway;
        _logger                  = logger;
    }

    public IObservable<RiskEventDto> RiskEvents =>
        _assessmentService.RiskEvents
            .Select(evt => new RiskEventDto
            {
                AirQualityLevel = EnumMapper.ToApp(evt.AirQualityLevel),
                Message         = evt.Message,
                Timestamp       = evt.Timestamp
            });

    public async Task<EnvironmentReadingDto> GetCurrentEnvironmentAsync()
    {
        var reading  = await _environmentDataProvider.GetCurrentAsync();
        var assessed = _assessmentService.AssessReading(reading);

        _ = _gateway.PersistAsync(assessed);

        return EnvironmentReadingMapper.ToDto(assessed);
    }

    public IObservable<EnvironmentReadingDto> SubscribeToEnvironment()
    {
        return _environmentDataProvider.SubscribeToUpdates()
            .Select(reading =>
            {
                var assessed = _assessmentService.AssessReading(reading);
                _ = _gateway.PersistAsync(assessed);
                return EnvironmentReadingMapper.ToDto(assessed);
            });
    }
}
