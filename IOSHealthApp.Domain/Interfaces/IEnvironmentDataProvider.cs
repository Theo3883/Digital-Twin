using IOSHealthApp.Domain.Models;

namespace IOSHealthApp.Domain.Interfaces;

public interface IEnvironmentDataProvider
{
    Task<EnvironmentReading> GetCurrentAsync();

    IObservable<EnvironmentReading> SubscribeToUpdates();
}
