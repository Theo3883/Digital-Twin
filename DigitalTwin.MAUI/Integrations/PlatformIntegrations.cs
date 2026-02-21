using DigitalTwin.Application.Configuration;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Integrations.Sync;
using DigitalTwin.Platform.Auth;

namespace DigitalTwin.Platform;

public static class PlatformIntegrations
{
    /// <summary>
    /// Registers MAUI platform-specific integrations that require
    /// WebAuthenticator, SecureStorage, or Connectivity APIs.
    /// Call after AddIntegrations() to override non-platform defaults.
    /// </summary>
    public static IServiceCollection AddMauiPlatformIntegrations(this IServiceCollection services, EnvConfig config)
    {
        services.AddScoped<IOAuthTokenProvider>(_ => new GoogleOAuthTokenProvider(
            config.GoogleOAuthClientId ?? "YOUR_CLIENT_ID",
            config.GoogleOAuthRedirectUri ?? "com.googleusercontent.apps.default:/oauth2redirect"));
        services.AddScoped<ISecureTokenStorage, SecureTokenStorage>();
        services.AddSingleton<ConnectivityMonitor>();

        return services;
    }
}
