using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface IEnvironmentDataProvider
{
    Task<EnvironmentReading> GetCurrentAsync();

    IObservable<EnvironmentReading> SubscribeToUpdates();
}
