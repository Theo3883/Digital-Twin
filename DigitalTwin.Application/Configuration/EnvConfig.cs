namespace DigitalTwin.Application.Configuration;

/// <summary>
/// Configuration loaded from environment variables (populated from .env or system env).
/// Presentation layer loads .env and passes this to Composition/Integrations.
/// </summary>
public class EnvConfig
{
    public static EnvConfig FromEnvironment()
    {
        return new EnvConfig
        {
            PostgresHost = Get("POSTGRES_HOST") ?? "localhost",
            PostgresPort = int.TryParse(Get("POSTGRES_PORT"), out var port) ? port : 5432,
            PostgresUser = Get("POSTGRES_USER") ?? "healthapp",
            PostgresPassword = Get("POSTGRES_PASSWORD") ?? "healthapp_dev",
            PostgresDb = Get("POSTGRES_DB") ?? "healthapp",
            GoogleOAuthClientId = Get("GOOGLE_OAUTH_CLIENT_ID"),
            GoogleOAuthRedirectUri = Get("GOOGLE_OAUTH_REDIRECT_URI"),
            OpenWeatherMapApiKey = Get("OPENWEATHERMAP_API_KEY"),
            GoogleAirQualityApiKey = Get("GOOGLE_AIR_QUALITY_API_KEY"),
            Latitude = double.TryParse(Get("LATITUDE"), System.Globalization.CultureInfo.InvariantCulture, out var lat) ? lat : 48.8566,
            Longitude = double.TryParse(Get("LONGITUDE"), System.Globalization.CultureInfo.InvariantCulture, out var lon) ? lon : 2.3522
        };
    }

    private static string? Get(string key) => Environment.GetEnvironmentVariable(key);

    public string PostgresHost { get; set; } = "localhost";
    public int PostgresPort { get; set; } = 5432;
    public string PostgresUser { get; set; } = "healthapp";
    public string PostgresPassword { get; set; } = "healthapp_dev";
    public string PostgresDb { get; set; } = "healthapp";

    public string? GoogleOAuthClientId { get; set; }
    public string? GoogleOAuthRedirectUri { get; set; }

    public string? OpenWeatherMapApiKey { get; set; }
    public string? GoogleAirQualityApiKey { get; set; }

    public double Latitude { get; set; } = 48.8566;
    public double Longitude { get; set; } = 2.3522;

    public string PostgresConnectionString =>
        $"Host={PostgresHost};Port={PostgresPort};Database={PostgresDb};Username={PostgresUser};Password={PostgresPassword}";

    public bool UseRealEnvironment => !string.IsNullOrWhiteSpace(OpenWeatherMapApiKey);
    public bool UseRealAirQuality => !string.IsNullOrWhiteSpace(GoogleAirQualityApiKey);
    public bool UseRealEnvironmentApis => UseRealEnvironment || UseRealAirQuality;
}
