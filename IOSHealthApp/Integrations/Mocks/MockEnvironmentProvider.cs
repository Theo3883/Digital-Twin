using System.Reactive.Linq;
using IOSHealthApp.Domain.Enums;
using IOSHealthApp.Domain.Interfaces;
using IOSHealthApp.Domain.Models;

namespace IOSHealthApp.Integrations.Mocks;

public class MockEnvironmentProvider : IEnvironmentDataProvider
{
    private readonly Random _random = new();

    public Task<EnvironmentReading> GetCurrentAsync()
    {
        return Task.FromResult(GenerateReading());
    }

    public IObservable<EnvironmentReading> SubscribeToUpdates()
    {
        return Observable.Interval(TimeSpan.FromSeconds(30))
            .Select(_ => GenerateReading());
    }

    private EnvironmentReading GenerateReading()
    {
        var pm25 = Math.Round(_random.NextDouble() * 150, 1);

        return new EnvironmentReading
        {
            PM25 = pm25,
            Temperature = Math.Round(18 + _random.NextDouble() * 15, 1),
            Humidity = Math.Round(30 + _random.NextDouble() * 50, 1),
            AirQuality = pm25 switch
            {
                <= 50 => AirQualityLevel.Good,
                <= 100 => AirQualityLevel.Moderate,
                _ => AirQualityLevel.Unhealthy
            },
            Timestamp = DateTime.UtcNow
        };
    }
}
