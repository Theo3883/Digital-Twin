using DigitalTwin.Application.Configuration;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Services;
using DigitalTwin.Integrations.Auth;
using DigitalTwin.Integrations.Environment;
using DigitalTwin.Integrations.Medication;
using DigitalTwin.Integrations.Mocks;
using DigitalTwin.Integrations.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Integrations;

public static class DependencyInjection
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services, EnvConfig config)
    {
        // Named HttpClient for Google OAuth/tokeninfo calls — 30 s timeout, no socket reuse leak.
        services.AddHttpClient("GoogleOAuth", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

#if IOS || MACCATALYST || ANDROID
        // Real platform implementations using MAUI Essentials APIs.
        services.AddScoped<IOAuthTokenProvider>(sp => new GoogleOAuthTokenProvider(
            config.GoogleOAuthClientId ?? "YOUR_CLIENT_ID",
            config.GoogleOAuthRedirectUri ?? "com.googleusercontent.apps.default:/oauth2redirect",
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<ILogger<GoogleOAuthTokenProvider>>()));
        services.AddScoped<ISecureTokenStorage, SecureTokenStorage>();
        services.AddSingleton<ConnectivityMonitor>();
#else
        // Non-platform fallbacks (unit tests, CI, desktop).
        services.AddScoped<IOAuthTokenProvider, GoogleOAuthTokenProvider>();
        services.AddScoped<ISecureTokenStorage, InMemoryTokenStorage>();
#endif

        services.AddScoped<IHealthDataProvider, MockHealthProvider>();

        if (config.UseRealEnvironmentApis)
        {
            services.AddSingleton(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                return new OpenWeatherMapProvider(factory.CreateClient(), config.OpenWeatherMapApiKey ?? string.Empty);
            });
            services.AddSingleton(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                // Air pollution uses the same OpenWeatherMap API key
                return new OpenWeatherAirQualityProvider(factory.CreateClient(), config.OpenWeatherMapApiKey ?? string.Empty);
            });
            services.AddScoped<IEnvironmentDataProvider>(sp => new HttpEnvironmentProvider(
                sp.GetRequiredService<OpenWeatherMapProvider>(),
                sp.GetRequiredService<OpenWeatherAirQualityProvider>(),
                sp.GetRequiredService<EnvironmentAssessmentService>(),
                config.Latitude,
                config.Longitude));
        }
        else
        {
            services.AddScoped<IEnvironmentDataProvider, MockEnvironmentProvider>();
        }

        services.AddScoped<ICoachingProvider, MockCoachingProvider>();

        services.AddHttpClient<IMedicationInteractionProvider, RxNavProvider>();

        return services;
    }
}

