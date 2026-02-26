using System.Reactive.Linq;
using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Mappers;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Services;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

public class EnvironmentApplicationService : IEnvironmentApplicationService
{
    private readonly IEnvironmentDataProvider _environmentDataProvider;
    private readonly EnvironmentAssessmentService _assessmentService;
    private readonly IEnvironmentReadingRepository _repository;
    private readonly ILogger<EnvironmentApplicationService> _logger;

    public EnvironmentApplicationService(
        IEnvironmentDataProvider environmentDataProvider,
        EnvironmentAssessmentService assessmentService,
        IEnvironmentReadingRepository repository,
        ILogger<EnvironmentApplicationService> logger)
    {
        _environmentDataProvider = environmentDataProvider;
        _assessmentService = assessmentService;
        _repository = repository;
        _logger = logger;
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

        _ = PersistAsync(assessed);

        return EnvironmentReadingMapper.ToDto(assessed);
    }

    public IObservable<EnvironmentReadingDto> SubscribeToEnvironment()
    {
        return _environmentDataProvider.SubscribeToUpdates()
            .Select(reading =>
            {
                var assessed = _assessmentService.AssessReading(reading);
                _ = PersistAsync(assessed);
                return EnvironmentReadingMapper.ToDto(assessed);
            });
    }

    private async Task PersistAsync(Domain.Models.EnvironmentReading reading)
    {
        try
        {
            await _repository.AddAsync(reading);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[EnvSync] Failed to persist environment reading locally.");
        }
    }
}
