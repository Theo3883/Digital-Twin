using Microsoft.Extensions.DependencyInjection;

namespace DigitalTwin.Services;

public static class LocationSearchServiceCollectionExtensions
{
    public static IServiceCollection AddLocationSearchService(
        this IServiceCollection services)
    {
        services.AddSingleton<ILocationSearchService, LocationSearchService>();
        return services;
    }
}
