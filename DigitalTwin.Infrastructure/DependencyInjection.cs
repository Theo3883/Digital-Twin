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
        services.AddKeyedScoped<INotificationRepository>(Cloud, (sp, _) =>
        {
            var f = sp.GetRequiredService<IDbContextFactory<CloudDbContext>>();
            return new NotificationRepository(() => f.CreateDbContext());
        });

        return services;
    }
}
