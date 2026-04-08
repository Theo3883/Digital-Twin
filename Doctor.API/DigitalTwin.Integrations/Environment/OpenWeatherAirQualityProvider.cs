using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigitalTwin.Integrations.Environment;

public class OpenWeatherAirQualityProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _apiKey;

    public OpenWeatherAirQualityProvider(HttpClient http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public async Task<AirQualityData?> GetAirQualityAsync(double lat, double lon)
    {
        if (string.IsNullOrWhiteSpace(_apiKey)) return null;
        try
        {
            var url = $"https://api.openweathermap.org/data/2.5/air_pollution?lat={lat}&lon={lon}&appid={_apiKey}";
            var response = await _http.GetFromJsonAsync<AirPollutionResponse>(url, JsonOptions);
            var entry = response?.List?.FirstOrDefault();
            if (entry is null) return null;

            return new AirQualityData
            {
                AqiIndex = entry.Main?.Aqi ?? 1,
                PM25 = entry.Components?.Pm2_5 ?? 0,
                PM10 = entry.Components?.Pm10 ?? 0,
                O3 = entry.Components?.O3 ?? 0,
                NO2 = entry.Components?.No2 ?? 0
            };
        }
        catch
        {
            return null;
        }
    }

    private sealed class AirPollutionResponse
    {
        [JsonPropertyName("list")]
        public List<AirEntry>? List { get; set; }
    }

    private sealed class AirEntry
    {
        [JsonPropertyName("main")]
        public AqiMain? Main { get; set; }

        [JsonPropertyName("components")]
        public Pollutants? Components { get; set; }
    }

    private sealed class AqiMain
    {
        [JsonPropertyName("aqi")]
        public int Aqi { get; set; }
    }

    private sealed class Pollutants
    {
        [JsonPropertyName("pm2_5")]
        public double Pm2_5 { get; set; }

        [JsonPropertyName("pm10")]
        public double Pm10 { get; set; }

        [JsonPropertyName("o3")]
        public double O3 { get; set; }

        [JsonPropertyName("no2")]
        public double No2 { get; set; }
    }
}
