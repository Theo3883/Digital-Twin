using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalTwin.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureLocal(
        this IServiceCollection services,
        string? connectionString = null)
    {
        var cs = connectionString ?? "Data Source=healthapp.db";

        services.AddDbContext<LocalDbContext>(options => options.UseSqlite(cs));

        services.AddScoped<IVitalSignRepository>(sp =>
            new VitalSignRepository(sp.GetRequiredService<LocalDbContext>()));
        services.AddScoped<IUserRepository>(sp =>
            new UserRepository(sp.GetRequiredService<LocalDbContext>()));
        services.AddScoped<IUserOAuthRepository>(sp =>
            new UserOAuthRepository(sp.GetRequiredService<LocalDbContext>()));
        services.AddScoped<IPatientRepository>(sp =>
            new PatientRepository(sp.GetRequiredService<LocalDbContext>()));

        return services;
    }

    public static IServiceCollection AddInfrastructureCloud(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<CloudDbContext>(options => options.UseNpgsql(connectionString));

        services.AddKeyedScoped<IVitalSignRepository>("Cloud", (sp, _) =>
            new VitalSignRepository(sp.GetRequiredService<CloudDbContext>()));
        services.AddKeyedScoped<IUserRepository>("Cloud", (sp, _) =>
            new UserRepository(sp.GetRequiredService<CloudDbContext>()));
        services.AddKeyedScoped<IUserOAuthRepository>("Cloud", (sp, _) =>
            new UserOAuthRepository(sp.GetRequiredService<CloudDbContext>()));
        services.AddKeyedScoped<IPatientRepository>("Cloud", (sp, _) =>
            new PatientRepository(sp.GetRequiredService<CloudDbContext>()));

        return services;
    }
}
