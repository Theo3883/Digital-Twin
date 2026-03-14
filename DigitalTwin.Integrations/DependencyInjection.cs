using DigitalTwin.Application.Configuration;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Integrations.AI;
using DigitalTwin.Integrations.Auth;
using DigitalTwin.Integrations.Ecg;
using DigitalTwin.Integrations.Environment;
using DigitalTwin.Integrations.Medication;
using DigitalTwin.Integrations.Mocks;
#if IOS || MACCATALYST
using DigitalTwin.Integrations.Sync;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#if IOS || MACCATALYST
using DigitalTwin.Integrations.HealthKit;
#endif

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

#if MOCK_HEALTH
        services.AddScoped<IHealthDataProvider, MockHealthProvider>();
#elif IOS || MACCATALYST
        services.AddScoped<IHealthDataProvider, HealthKitProvider>();
#else
        services.AddScoped<IHealthDataProvider, MockHealthProvider>();
#endif

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
            sp.GetRequiredService<IEnvironmentAssessmentService>(),
            config.Latitude,
            config.Longitude));

        // ── Gemini AI: chatbot + coaching ────────────────────────────────────
        services.Configure<GeminiPromptOptions>(_ => { }); // Register with defaults

        services.AddHttpClient("GeminiApi", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        if (config.UseGeminiAi)
        {
            services.AddSingleton<IGeminiApiClient>(sp => new GeminiApiClient(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<IOptions<GeminiPromptOptions>>(),
                sp.GetRequiredService<ILogger<GeminiApiClient>>(),
                config.GeminiApiKey!));

            services.AddScoped<IChatBotProvider, GeminiChatBotProvider>();
            services.AddScoped<ICoachingProvider, GeminiCoachingProvider>();
            services.AddScoped<IRxCuiLookupProvider, GeminiRxCuiLookupProvider>();
        }
        else
        {
            services.AddScoped<IChatBotProvider, MockChatBotProvider>();
            services.AddScoped<ICoachingProvider, MockCoachingProvider>();
            services.AddScoped<IRxCuiLookupProvider, NullRxCuiLookupProvider>();
        }

        services.AddHttpClient<IMedicationInteractionProvider, RxNavProvider>();
        services.AddHttpClient<IDrugSearchProvider, RxNavProvider>();

        // ECG: direct connection to ESP32 on local network.
        // EcgDeviceUrl is set via ECG_DEVICE_URL env var or entered by the user at runtime.
        services.AddSingleton<IEcgStreamProvider, EcgStreamClient>();

        return services;
    }
}
