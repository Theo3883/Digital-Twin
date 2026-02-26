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

        // AddDbContextFactory registers IDbContextFactory<T> as a singleton.
        // Every call to factory.CreateDbContext() returns a BRAND NEW DbContext instance
        // that the caller owns and must dispose — eliminating all concurrent-context crashes.
        services.AddDbContextFactory<LocalDbContext>(options => options.UseSqlite(cs));

        // Each repo receives a Func<HealthAppDbContext> that creates a fresh context per call.
        // This means the repos themselves are stateless and safe to inject into any lifetime.
        services.AddScoped<IVitalSignRepository>(sp =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<LocalDbContext>>();
            return new VitalSignRepository(() => f.CreateDbContext());
        });
        services.AddScoped<IUserRepository>(sp =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<LocalDbContext>>();
            return new UserRepository(() => f.CreateDbContext());
        });
        services.AddScoped<IUserOAuthRepository>(sp =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<LocalDbContext>>();
            return new UserOAuthRepository(() => f.CreateDbContext());
        });
        services.AddScoped<IPatientRepository>(sp =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<LocalDbContext>>();
            return new PatientRepository(() => f.CreateDbContext());
        });
        services.AddScoped<IEnvironmentReadingRepository>(sp =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<LocalDbContext>>();
            return new EnvironmentReadingRepository(() => f.CreateDbContext());
        });

        return services;
    }

    public static IServiceCollection AddInfrastructureCloud(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContextFactory<CloudDbContext>(options => options.UseNpgsql(connectionString));

        services.AddKeyedScoped<IVitalSignRepository>("Cloud", (sp, _) =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<CloudDbContext>>();
            return new VitalSignRepository(() => f.CreateDbContext(), markDirtyOnInsert: false);
        });
        services.AddKeyedScoped<IUserRepository>("Cloud", (sp, _) =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<CloudDbContext>>();
            return new UserRepository(() => f.CreateDbContext(), markDirtyOnInsert: false);
        });
        services.AddKeyedScoped<IUserOAuthRepository>("Cloud", (sp, _) =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<CloudDbContext>>();
            return new UserOAuthRepository(() => f.CreateDbContext(), markDirtyOnInsert: false);
        });
        services.AddKeyedScoped<IPatientRepository>("Cloud", (sp, _) =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<CloudDbContext>>();
            return new PatientRepository(() => f.CreateDbContext(), markDirtyOnInsert: false);
        });
        services.AddKeyedScoped<IEnvironmentReadingRepository>("Cloud", (sp, _) =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<CloudDbContext>>();
            return new EnvironmentReadingRepository(() => f.CreateDbContext(), markDirtyOnInsert: false);
        });

        return services;
    }
}
