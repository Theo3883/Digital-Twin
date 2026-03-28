using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DigitalTwin.Integrations.Environment;

/// <summary>
/// OpenWeather Geocoding API 1.0 — direct geocoding (city name → coordinates).
/// </summary>
public sealed class OpenWeatherGeocodingClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public OpenWeatherGeocodingClient(HttpClient http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    /// <summary>
    /// Returns the first matching place, or <see langword="null"/> if none or API key missing.
    /// </summary>
    public async Task<OpenWeatherGeocodeHit?> GeocodeFirstAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(query))
            return null;

        var q = Uri.EscapeDataString(query.Trim());
        var url = $"https://api.openweathermap.org/geo/1.0/direct?q={q}&limit=5&appid={_apiKey}";
        try
        {
            var list = await _http.GetFromJsonAsync<List<OpenWeatherGeocodeHitDto>>(url, cancellationToken).ConfigureAwait(false);
            if (list is null || list.Count == 0)
                return null;

            var h = list[0];
            return new OpenWeatherGeocodeHit(h.Lat, h.Lon, FormatDisplayName(h));
        }
        catch
        {
            return null;
        }
    }

    private static string FormatDisplayName(OpenWeatherGeocodeHitDto h)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(h.LocalNames?.En))
            parts.Add(h.LocalNames.En);
        else if (!string.IsNullOrWhiteSpace(h.Name))
            parts.Add(h.Name);

        if (!string.IsNullOrWhiteSpace(h.State))
            parts.Add(h.State);

        if (!string.IsNullOrWhiteSpace(h.Country))
            parts.Add(h.Country);

        return parts.Count > 0 ? string.Join(", ", parts) : (h.Name ?? "Unknown");
    }

    private sealed class OpenWeatherGeocodeHitDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("local_names")]
        public LocalNamesDto? LocalNames { get; set; }
    }

    private sealed class LocalNamesDto
    {
        [JsonPropertyName("en")]
        public string? En { get; set; }
    }
}

public sealed record OpenWeatherGeocodeHit(double Latitude, double Longitude, string DisplayName);
