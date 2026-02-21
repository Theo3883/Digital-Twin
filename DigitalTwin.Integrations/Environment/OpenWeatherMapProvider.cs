using System.Net.Http.Json;

namespace DigitalTwin.Integrations.Environment;

public class WeatherData
{
    public double Temperature { get; set; }
    public double Humidity { get; set; }
}

public class OpenWeatherMapProvider
{
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
            var response = await _http.GetFromJsonAsync<OpenWeatherResponse>(url);
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
        public MainData? Main { get; set; }
    }

    private class MainData
    {
        public double Temp { get; set; }
        public double Humidity { get; set; }
    }
}
