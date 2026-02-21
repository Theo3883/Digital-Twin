using DigitalTwin.Application;
using DigitalTwin.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalTwin.Composition;

public static class DependencyInjection
{
    /// <summary>
    /// Registers all layers: Infrastructure, Application, and Domain services.
    /// The <paramref name="registerIntegrations"/> callback allows the presentation layer
    /// to register platform-specific providers (mocks, HealthKit, HTTP, etc.) without
    /// Composition referencing the presentation project.
    /// </summary>
    public static IServiceCollection AddDigitalTwin(
        this IServiceCollection services,
        Action<IServiceCollection>? registerIntegrations = null)
    {
        services.AddInfrastructure();
        services.AddApplication();

        registerIntegrations?.Invoke(services);

        return services;
    }
}
