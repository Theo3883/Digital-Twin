using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

public interface IEnvironmentApplicationService
{
    Task<EnvironmentReadingDto> GetCurrentEnvironmentAsync();

    IObservable<EnvironmentReadingDto> SubscribeToEnvironment();

    IObservable<RiskEventDto> RiskEvents { get; }
}
