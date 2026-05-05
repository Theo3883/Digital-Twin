using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Infrastructure.Services;

public class EnvironmentDataService : IEnvironmentDataProvider
{
    private readonly HttpClient _weatherClient;
    private readonly HttpClient _aqClient;
    private readonly string _apiKey;
    private readonly ILogger<EnvironmentDataService> _logger;

    public EnvironmentDataService(
        IHttpClientFactory httpClientFactory,
        string apiKey,
        ILogger<EnvironmentDataService> logger)
    {
        _weatherClient = httpClientFactory.CreateClient("OpenWeather");
        _aqClient = httpClientFactory.CreateClient("OpenWeatherAQ");
        _apiKey = apiKey;
        _logger = logger;
    }

    public async Task<EnvironmentReading> GetCurrentAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        var reading = new EnvironmentReading
        {
            Latitude = latitude,
            Longitude = longitude
        };

        // Fetch weather
        try
        {
            var weatherUrl = $"?lat={latitude}&lon={longitude}&appid={_apiKey}&units=metric";
            await using var wStream = await _weatherClient.GetStreamAsync(weatherUrl, ct);
            var weather = await JsonSerializer.DeserializeAsync(wStream, IntegrationJsonContext.Default.OpenWeatherResponse, ct);

            if (weather?.Main != null)
            {
                reading.Temperature = weather.Main.Temp;
                reading.Humidity = weather.Main.Humidity;
            }

            reading.LocationDisplayName = weather?.Name ?? $"{latitude:F2}, {longitude:F2}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Environment] Weather fetch failed");
        }

        // Fetch air quality
        try
        {
            var aqUrl = $"?lat={latitude}&lon={longitude}&appid={_apiKey}";
            await using var aqStream = await _aqClient.GetStreamAsync(aqUrl, ct);
            var aq = await JsonSerializer.DeserializeAsync(aqStream, IntegrationJsonContext.Default.OpenWeatherAqResponse, ct);

            var data = aq?.List?.FirstOrDefault();
            if (data != null)
            {
                reading.AqiIndex = data.Main?.Aqi ?? 0;
                reading.PM25 = data.Components?.Pm2_5 ?? 0;
                reading.PM10 = data.Components?.Pm10 ?? 0;
                reading.O3 = data.Components?.O3 ?? 0;
                reading.NO2 = data.Components?.No2 ?? 0;

                reading.AirQuality = reading.AqiIndex switch
                {
                    1 => AirQualityLevel.Good,
                    2 => AirQualityLevel.Moderate,
                    3 or 4 => AirQualityLevel.Unhealthy,
                    _ => AirQualityLevel.Unhealthy
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Environment] Air quality fetch failed");
        }

        return reading;
    }
}

// OpenWeather JSON models
public sealed record OpenWeatherResponse
{
    [JsonPropertyName("main")]
    public OpenWeatherMain? Main { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

public sealed record OpenWeatherMain
{
    [JsonPropertyName("temp")]
    public double Temp { get; init; }

    [JsonPropertyName("humidity")]
    public double Humidity { get; init; }
}

public sealed record OpenWeatherAqResponse
{
    [JsonPropertyName("list")]
    public List<OpenWeatherAqData>? List { get; init; }
}

public sealed record OpenWeatherAqData
{
    [JsonPropertyName("main")]
    public OpenWeatherAqMain? Main { get; init; }

    [JsonPropertyName("components")]
    public OpenWeatherAqComponents? Components { get; init; }
}

public sealed record OpenWeatherAqMain
{
    [JsonPropertyName("aqi")]
    public int Aqi { get; init; }
}

public sealed record OpenWeatherAqComponents
{
    [JsonPropertyName("pm2_5")]
    public double Pm2_5 { get; init; }

    [JsonPropertyName("pm10")]
    public double Pm10 { get; init; }

    [JsonPropertyName("o3")]
    public double O3 { get; init; }

    [JsonPropertyName("no2")]
    public double No2 { get; init; }
}
