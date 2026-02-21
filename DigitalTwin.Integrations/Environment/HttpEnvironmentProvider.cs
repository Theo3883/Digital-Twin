using System.Reactive.Linq;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using DigitalTwin.Domain.Services;

namespace DigitalTwin.Integrations.Environment;

public class HttpEnvironmentProvider : IEnvironmentDataProvider
{
    private readonly OpenWeatherMapProvider _weather;
    private readonly GoogleAirQualityProvider _airQuality;
    private readonly EnvironmentAssessmentService _assessmentService;
    private readonly double _latitude;
    private readonly double _longitude;

    public HttpEnvironmentProvider(
        OpenWeatherMapProvider weather,
        GoogleAirQualityProvider airQuality,
        EnvironmentAssessmentService assessmentService,
        double latitude = 48.8566,
        double longitude = 2.3522)
    {
        _weather = weather;
        _airQuality = airQuality;
        _assessmentService = assessmentService;
        _latitude = latitude;
        _longitude = longitude;
    }

    public async Task<EnvironmentReading> GetCurrentAsync()
    {
        var weatherTask = _weather.GetWeatherAsync(_latitude, _longitude);
        var airTask = _airQuality.GetAirQualityAsync(_latitude, _longitude);

        await Task.WhenAll(weatherTask, airTask);

        var weather = await weatherTask;
        var air = await airTask;

        var reading = new EnvironmentReading
        {
            Temperature = weather?.Temperature ?? 0,
            Humidity = weather?.Humidity ?? 0,
            PM25 = air?.PM25 ?? 0,
            Timestamp = DateTime.UtcNow
        };

        return _assessmentService.AssessReading(reading);
    }

    public IObservable<EnvironmentReading> SubscribeToUpdates()
    {
        return Observable.Interval(TimeSpan.FromMinutes(5))
            .SelectMany(_ => Observable.FromAsync(GetCurrentAsync));
    }
}
