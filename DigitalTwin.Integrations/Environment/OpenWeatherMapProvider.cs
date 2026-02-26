using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigitalTwin.Integrations.Environment;

public class OpenWeatherMapProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _apiKey;

    public OpenWeatherMapProvider(HttpClient http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public async Task<WeatherData?> GetWeatherAsync(double lat, double lon)
    {
        if (string.IsNullOrWhiteSpace(_apiKey)) return null;
        try
        {
            var url = $"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&appid={_apiKey}&units=metric";
            var response = await _http.GetFromJsonAsync<OpenWeatherResponse>(url, JsonOptions);
            if (response is null) return null;

            return new WeatherData
            {
                Temperature = response.Main?.Temp ?? 0,
                Humidity = response.Main?.Humidity ?? 0
            };
        }
        catch
        {
            return null;
        }
    }

    private class OpenWeatherResponse
    {
        [JsonPropertyName("main")]
        public MainData? Main { get; set; }
    }

    private class MainData
    {
        [JsonPropertyName("temp")]
        public double Temp { get; set; }

        [JsonPropertyName("humidity")]
        public double Humidity { get; set; }
    }
}
