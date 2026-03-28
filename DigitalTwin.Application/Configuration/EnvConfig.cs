namespace DigitalTwin.Application.Configuration;

/// <summary>
/// Stores application configuration values loaded from environment variables.
/// </summary>
public class EnvConfig
{
    private const string DefaultDbName = "healthapp";

    /// <summary>
    /// Creates a configuration instance from the current process environment.
    /// </summary>
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
            Longitude = double.TryParse(Get("LONGITUDE"), System.Globalization.CultureInfo.InvariantCulture, out var lon) ? lon : 2.3522,
            EcgDeviceUrl = Get("ECG_DEVICE_URL"),
            GeminiApiKey = Get("GEMINI_API_KEY")
        };
    }

    private static string? Get(string key) => Environment.GetEnvironmentVariable(key);

    /// <summary>
    /// Gets or sets the PostgreSQL server host name.
    /// </summary>
    public string PostgresHost { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the PostgreSQL server port.
    /// </summary>
    public int PostgresPort { get; set; } = 5432;

    /// <summary>
    /// Gets or sets the PostgreSQL user name.
    /// </summary>
    public string PostgresUser { get; set; } = DefaultDbName;

    /// <summary>
    /// Gets or sets the PostgreSQL password.
    /// When not configured, the cloud database connection string is not produced.
    /// </summary>
    public string? PostgresPassword { get; set; }

    /// <summary>
    /// Gets or sets the PostgreSQL database name.
    /// </summary>
    public string PostgresDb { get; set; } = DefaultDbName;

    /// <summary>
    /// Gets or sets the Google OAuth client identifier.
    /// </summary>
    public string? GoogleOAuthClientId { get; set; }

    /// <summary>
    /// Gets or sets the Google OAuth redirect URI.
    /// </summary>
    public string? GoogleOAuthRedirectUri { get; set; }

    /// <summary>
    /// Gets or sets the OpenWeatherMap API key used for environment data.
    /// </summary>
    public string? OpenWeatherMapApiKey { get; set; }

    /// <summary>
    /// Gets or sets the Google Air Quality API key.
    /// </summary>
    public string? GoogleAirQualityApiKey { get; set; }

    /// <summary>
    /// Gets or sets a fallback latitude when the app cannot use GPS or a user-entered city
    /// (simulator, denied permission, or non-mobile host). The MAUI app normally uses device location or manual geocoding instead.
    /// </summary>
    public double Latitude { get; set; } = 48.8566;

    /// <summary>
    /// Gets or sets a fallback longitude; see <see cref="Latitude"/>.
    /// </summary>
    public double Longitude { get; set; } = 2.3522;

    /// <summary>
    /// Gets or sets the WebSocket URL for the ECG device.
    /// </summary>
    public string? EcgDeviceUrl { get; set; }

    /// <summary>
    /// Gets or sets the Gemini API key used for AI-backed features.
    /// </summary>
    public string? GeminiApiKey { get; set; }

    /// <summary>
    /// Gets the PostgreSQL connection string when a password is configured; otherwise returns <see langword="null"/>.
    /// </summary>
    public string? PostgresConnectionString =>
        PostgresPassword is null
            ? null
            : $"Host={PostgresHost};Port={PostgresPort};Database={PostgresDb};Username={PostgresUser};Password={PostgresPassword}";

    /// <summary>
    /// Gets a value indicating whether OpenWeatherMap integration is enabled.
    /// </summary>
    public bool UseRealEnvironment => !string.IsNullOrWhiteSpace(OpenWeatherMapApiKey);

    /// <summary>
    /// Gets a value indicating whether Google Air Quality integration is enabled.
    /// </summary>
    public bool UseRealAirQuality => !string.IsNullOrWhiteSpace(GoogleAirQualityApiKey);

    /// <summary>
    /// Gets a value indicating whether any real environment provider is enabled.
    /// </summary>
    public bool UseRealEnvironmentApis => UseRealEnvironment || UseRealAirQuality;

    /// <summary>
    /// Gets a value indicating whether Gemini-backed AI features are enabled.
    /// </summary>
    public bool UseGeminiAi => !string.IsNullOrWhiteSpace(GeminiApiKey);
}
