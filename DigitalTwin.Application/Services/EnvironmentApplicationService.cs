using System.Reactive.Linq;
using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Mappers;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Orchestrates environment retrieval, assessment, persistence, and risk-event projection.
/// </summary>
public class EnvironmentApplicationService : IEnvironmentApplicationService
{
    private readonly IEnvironmentDataProvider                         _environmentDataProvider;
    private readonly IEnvironmentAssessmentService                    _assessmentService;
    private readonly IPersistenceGateway<Domain.Models.EnvironmentReading> _gateway;
    private readonly ILogger<EnvironmentApplicationService>           _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentApplicationService"/> class.
    /// </summary>
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

    /// <summary>
    /// Gets the stream of risk events generated from assessed environment readings.
    /// </summary>
    public IObservable<RiskEventDto> RiskEvents =>
        _assessmentService.RiskEvents
            .Select(evt => new RiskEventDto
            {
                AirQualityLevel = EnumMapper.ToApp(evt.AirQualityLevel),
                Message         = evt.Message,
                Timestamp       = evt.Timestamp
            });

    /// <summary>
    /// Gets the current environment reading after assessment and persistence.
    /// </summary>
    public async Task<EnvironmentReadingDto> GetCurrentEnvironmentAsync()
    {
        var reading  = await _environmentDataProvider.GetCurrentAsync();
        var assessed = _assessmentService.AssessReading(reading);

        _ = _gateway.PersistAsync(assessed);

        return EnvironmentReadingMapper.ToDto(assessed);
    }

    /// <summary>
    /// Subscribes to assessed environment updates and persists each reading.
    /// </summary>
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
