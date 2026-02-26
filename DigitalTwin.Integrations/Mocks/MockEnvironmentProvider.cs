using System.Reactive.Linq;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Integrations.Mocks;

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
        var pm25 = Math.Round(_random.NextDouble() * 50, 1);
        var aqiIndex = pm25 switch { <= 12 => 1, <= 35 => 2, _ => 3 };

        return new EnvironmentReading
        {
            PM25 = pm25,
            PM10 = Math.Round(pm25 * 1.4 + _random.NextDouble() * 5, 1),
            O3 = Math.Round(30 + _random.NextDouble() * 40, 1),
            NO2 = Math.Round(5 + _random.NextDouble() * 30, 1),
            Temperature = Math.Round(18 + _random.NextDouble() * 15, 1),
            Humidity = Math.Round(30 + _random.NextDouble() * 50, 1),
            AqiIndex = aqiIndex,
            AirQuality = aqiIndex switch
            {
                1 or 2 => AirQualityLevel.Good,
                3 => AirQualityLevel.Moderate,
                _ => AirQualityLevel.Unhealthy
            },
            Timestamp = DateTime.UtcNow
        };
    }
}
