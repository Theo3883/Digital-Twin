using IOSHealthApp.Domain.Interfaces;
using IOSHealthApp.Infrastructure.Data;
using IOSHealthApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IOSHealthApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? connectionString = null)
    {
        services.AddDbContext<HealthAppDbContext>(options =>
        {
            var cs = connectionString ?? "Data Source=healthapp.db";
            options.UseSqlite(cs);
        });

        services.AddScoped<IVitalSignRepository, VitalSignRepository>();

        return services;
    }
}
