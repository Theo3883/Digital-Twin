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
    private readonly IEnvironmentLocationSource _locationSource;

    public HttpEnvironmentProvider(
        OpenWeatherMapProvider weather,
        OpenWeatherAirQualityProvider airQuality,
        IEnvironmentAssessmentService assessmentService,
        IEnvironmentLocationSource locationSource)
    {
        _weather = weather;
        _airQuality = airQuality;
        _assessmentService = assessmentService;
        _locationSource = locationSource;
    }

    public async Task<EnvironmentReading> GetCurrentAsync()
    {
        var loc = await _locationSource.ResolveAsync(CancellationToken.None).ConfigureAwait(false);

        var weatherTask = _weather.GetWeatherAsync(loc.Latitude, loc.Longitude);
        var airTask = _airQuality.GetAirQualityAsync(loc.Latitude, loc.Longitude);

        await Task.WhenAll(weatherTask, airTask).ConfigureAwait(false);

        var weather = await weatherTask.ConfigureAwait(false);
        var air = await airTask.ConfigureAwait(false);

        var reading = new EnvironmentReading
        {
            Latitude = loc.Latitude,
            Longitude = loc.Longitude,
            LocationDisplayName = loc.DisplayName,
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
