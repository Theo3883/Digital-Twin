using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.Infrastructure.Repositories;
using DigitalTwin.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DigitalTwin.Infrastructure;

public static class DependencyInjection
{
    private const string Cloud = "Cloud";
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
        services.AddScoped<ISleepSessionRepository>(sp =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<LocalDbContext>>();
            return new SleepSessionRepository(() => f.CreateDbContext());
        });
        services.AddScoped<IDoctorPatientAssignmentRepository>(sp =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<LocalDbContext>>();
            return new DoctorPatientAssignmentRepository(() => f.CreateDbContext());
        });
        services.AddScoped<IMedicationRepository>(sp =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<LocalDbContext>>();
            return new MedicationRepository(() => f.CreateDbContext());
        });
        services.AddScoped<IOcrDocumentRepository>(sp =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<LocalDbContext>>();
            return new OcrDocumentRepository(() => f.CreateDbContext());
        });
        services.AddScoped<IMedicalHistoryEntryRepository>(sp =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<LocalDbContext>>();
            return new MedicalHistoryEntryRepository(() => f.CreateDbContext());
        });

        return services;
    }

    public static IServiceCollection AddInfrastructureCloud(
        this IServiceCollection services,
        string connectionString)
    {
        // Register the cloud circuit-breaker singleton so all cloud callers can
        // skip queries instantly when the DB is known to be unreachable.
        var csb = new NpgsqlConnectionStringBuilder(connectionString);
        services.AddSingleton<ICloudHealthService>(sp => new CloudHealthService(
            csb.Host ?? "localhost",
            csb.Port,
            sp.GetRequiredService<ILogger<CloudHealthService>>()));

        services.AddDbContextFactory<CloudDbContext>(options => options.UseNpgsql(connectionString));

        services.AddKeyedScoped<IVitalSignRepository>(Cloud, (sp, _) =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<CloudDbContext>>();
            return new VitalSignRepository(() => f.CreateDbContext(), markDirtyOnInsert: false);
        });
        services.AddKeyedScoped<IUserRepository>(Cloud, (sp, _) =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<CloudDbContext>>();
            return new UserRepository(() => f.CreateDbContext(), markDirtyOnInsert: false);
        });
        services.AddKeyedScoped<IUserOAuthRepository>(Cloud, (sp, _) =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<CloudDbContext>>();
            return new UserOAuthRepository(() => f.CreateDbContext(), markDirtyOnInsert: false);
        });
        services.AddKeyedScoped<IPatientRepository>(Cloud, (sp, _) =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<CloudDbContext>>();
            return new PatientRepository(() => f.CreateDbContext(), markDirtyOnInsert: false);
        });
        services.AddKeyedScoped<IEnvironmentReadingRepository>(Cloud, (sp, _) =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<CloudDbContext>>();
            return new EnvironmentReadingRepository(() => f.CreateDbContext(), markDirtyOnInsert: false);
        });
        services.AddKeyedScoped<ISleepSessionRepository>(Cloud, (sp, _) =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<CloudDbContext>>();
            return new SleepSessionRepository(() => f.CreateDbContext(), markDirtyOnInsert: false);
        });
        services.AddKeyedScoped<IDoctorPatientAssignmentRepository>(Cloud, (sp, _) =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<CloudDbContext>>();
            return new DoctorPatientAssignmentRepository(() => f.CreateDbContext(), markDirtyOnInsert: false);
        });
        services.AddKeyedScoped<IMedicationRepository>(Cloud, (sp, _) =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<CloudDbContext>>();
            return new MedicationRepository(() => f.CreateDbContext(), markDirtyOnInsert: false);
        });
        services.AddKeyedScoped<IOcrDocumentRepository>(Cloud, (sp, _) =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<CloudDbContext>>();
            return new OcrDocumentRepository(() => f.CreateDbContext(), markDirtyOnInsert: false);
        });
        services.AddKeyedScoped<IMedicalHistoryEntryRepository>(Cloud, (sp, _) =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<CloudDbContext>>();
            return new MedicalHistoryEntryRepository(() => f.CreateDbContext(), markDirtyOnInsert: false);
        });

        return services;
    }
}
