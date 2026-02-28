using System.Reactive.Linq;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Integrations.Environment;

public class HttpEnvironmentProvider : IEnvironmentDataProvider
{
    private readonly OpenWeatherMapProvider _weather;
    private readonly OpenWeatherAirQualityProvider _airQuality;
    private readonly IEnvironmentAssessmentService _assessmentService;
    private readonly double _latitude;
    private readonly double _longitude;

    public HttpEnvironmentProvider(
        OpenWeatherMapProvider weather,
        OpenWeatherAirQualityProvider airQuality,
        IEnvironmentAssessmentService assessmentService,
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
            Latitude = _latitude,
            Longitude = _longitude,
            Temperature = weather?.Temperature ?? 0,
            Humidity = weather?.Humidity ?? 0,
            PM25 = air?.PM25 ?? 0,
            PM10 = air?.PM10 ?? 0,
            O3 = air?.O3 ?? 0,
            NO2 = air?.NO2 ?? 0,
            AqiIndex = air?.AqiIndex ?? 1,
            Timestamp = DateTime.UtcNow
        };

        // Derive AirQualityLevel from the OpenWeather AQI index (1–5)
        reading.AirQuality = reading.AqiIndex switch
        {
            1 or 2 => AirQualityLevel.Good,
            3 => AirQualityLevel.Moderate,
            4 or 5 => AirQualityLevel.Unhealthy,
            _ => _assessmentService.DetermineAirQuality(reading.PM25)
        };

        // Still fire domain risk events for unhealthy readings
        _assessmentService.AssessReading(reading);

        return reading;
    }

    public IObservable<EnvironmentReading> SubscribeToUpdates()
    {
        return Observable.Interval(TimeSpan.FromSeconds(30))
            .SelectMany(_ => Observable.FromAsync(GetCurrentAsync));
    }
}
