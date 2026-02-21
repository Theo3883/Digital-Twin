using DigitalTwin.Application.Configuration;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Services;
using DigitalTwin.Integrations.Auth;
using DigitalTwin.Integrations.Environment;
using DigitalTwin.Integrations.Medication;
using DigitalTwin.Integrations.Mocks;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalTwin.Integrations;

public static class DependencyInjection
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services, EnvConfig config)
    {
        services.AddScoped<IOAuthTokenProvider, GoogleOAuthTokenProvider>();
        services.AddScoped<ISecureTokenStorage, InMemoryTokenStorage>();

        services.AddScoped<IHealthDataProvider, MockHealthProvider>();

        if (config.UseRealEnvironmentApis)
        {
            services.AddSingleton(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                return new OpenWeatherMapProvider(
                    factory.CreateClient(),
                    config.OpenWeatherMapApiKey ?? string.Empty);
            });
            services.AddSingleton(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                return new GoogleAirQualityProvider(
                    factory.CreateClient(),
                    config.GoogleAirQualityApiKey ?? string.Empty);
            });
            services.AddScoped<IEnvironmentDataProvider>(sp => new HttpEnvironmentProvider(
                sp.GetRequiredService<OpenWeatherMapProvider>(),
                sp.GetRequiredService<GoogleAirQualityProvider>(),
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
