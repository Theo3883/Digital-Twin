using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface IEnvironmentDataProvider
{
    Task<EnvironmentReading> GetCurrentAsync(double latitude, double longitude, CancellationToken ct = default);
}
