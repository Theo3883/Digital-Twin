using System.Reactive.Linq;
using IOSHealthApp.Application.DTOs;
using IOSHealthApp.Application.Interfaces;
using IOSHealthApp.Application.Mappers;
using IOSHealthApp.Domain.Interfaces;
using IOSHealthApp.Domain.Services;

namespace IOSHealthApp.Application.Services;

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
