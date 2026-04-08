using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Providers;

public interface IEnvironmentDataProvider
{
    Task<EnvironmentReading> GetCurrentAsync();

    IObservable<EnvironmentReading> SubscribeToUpdates();
}
