using System.Net.Http.Json;

namespace DigitalTwin.Integrations.Environment;

public class AirQualityData
{
    public double PM25 { get; set; }
    public int AqiIndex { get; set; }
}

public class GoogleAirQualityProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public GoogleAirQualityProvider(HttpClient http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public async Task<AirQualityData?> GetAirQualityAsync(double lat, double lon)
    {
        if (string.IsNullOrWhiteSpace(_apiKey)) return null;
        try
        {
            var requestBody = new
            {
                location = new { latitude = lat, longitude = lon },
                extraComputations = new[] { "POLLUTANT_CONCENTRATION" }
            };

            var response = await _http.PostAsJsonAsync(
                $"https://airquality.googleapis.com/v1/currentConditions:lookup?key={_apiKey}",
                requestBody);

            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<AirQualityResponse>();
            var pm25Pollutant = result?.Indexes?.FirstOrDefault();

            return new AirQualityData
            {
                AqiIndex = pm25Pollutant?.Aqi ?? 0,
                PM25 = pm25Pollutant?.Aqi ?? 0
            };
        }
        catch
        {
            return null;
        }
    }

    private class AirQualityResponse
    {
        public List<AqiIndex>? Indexes { get; set; }
    }

    private class AqiIndex
    {
        public int Aqi { get; set; }
    }
}
