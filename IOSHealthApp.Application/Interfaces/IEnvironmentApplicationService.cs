using IOSHealthApp.Application.DTOs;

namespace IOSHealthApp.Application.Interfaces;

public interface IEnvironmentApplicationService
{
    Task<EnvironmentReadingDto> GetCurrentEnvironmentAsync();

    IObservable<EnvironmentReadingDto> SubscribeToEnvironment();
}
