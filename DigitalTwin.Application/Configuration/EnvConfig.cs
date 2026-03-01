namespace DigitalTwin.Application.Configuration;

/// <summary>
/// Configuration loaded from environment variables (populated from .env or system env).
/// Presentation layer loads .env and passes this to Composition/Integrations.
/// </summary>
public class EnvConfig
{
    private const string DefaultDbName = "healthapp";

    public static EnvConfig FromEnvironment()
    {
        return new EnvConfig
        {
            PostgresHost = Get("POSTGRES_HOST") ?? "localhost",
            PostgresPort = int.TryParse(Get("POSTGRES_PORT"), out var port) ? port : 5432,
            PostgresUser = Get("POSTGRES_USER") ?? DefaultDbName,
            PostgresPassword = Get("POSTGRES_PASSWORD"),   // null if not set — no insecure default
            PostgresDb = Get("POSTGRES_DB") ?? DefaultDbName,
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
    public string PostgresUser { get; set; } = DefaultDbName;

    /// <summary>
    /// No default — must be set via POSTGRES_PASSWORD environment variable.
    /// If null, <see cref="PostgresConnectionString"/> returns null and cloud DB is skipped.
    /// </summary>
    public string? PostgresPassword { get; set; }

    public string PostgresDb { get; set; } = DefaultDbName;

    public string? GoogleOAuthClientId { get; set; }
    public string? GoogleOAuthRedirectUri { get; set; }

    public string? OpenWeatherMapApiKey { get; set; }
    public string? GoogleAirQualityApiKey { get; set; }

    public double Latitude { get; set; } = 48.8566;
    public double Longitude { get; set; } = 2.3522;

    /// <summary>
    /// Returns a PostgreSQL connection string, or <see langword="null"/> if
    /// <see cref="PostgresPassword"/> has not been configured.
    /// A null value causes <c>AddDigitalTwin</c> to skip cloud DB registration entirely.
    /// </summary>
    public string? PostgresConnectionString =>
        PostgresPassword is null
            ? null
            : $"Host={PostgresHost};Port={PostgresPort};Database={PostgresDb};Username={PostgresUser};Password={PostgresPassword}";

    public bool UseRealEnvironment => !string.IsNullOrWhiteSpace(OpenWeatherMapApiKey);
    public bool UseRealAirQuality => !string.IsNullOrWhiteSpace(GoogleAirQualityApiKey);
    public bool UseRealEnvironmentApis => UseRealEnvironment || UseRealAirQuality;
}
