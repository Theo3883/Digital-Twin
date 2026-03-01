using FluentValidation;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Services;
using DigitalTwin.Application.Sync.Drainers;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Services;
using DigitalTwin.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Composition;

/// <summary>
/// Central DI hub. Every platform calls one of the <c>AddDigitalTwinFor…</c>
/// methods — no layer registers its own services.
/// </summary>
public static class DependencyInjection
{
    private const string Cloud = "Cloud";
    // ═══════════════════════════════════════════════════════════════════════════
    //  Web API  (cloud-only, no device providers)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Registers services for the ASP.NET Core Web API host.
    /// Uses only the cloud (PostgreSQL) database — no local SQLite, no device
    /// providers, no table drainers.
    /// </summary>
    public static IServiceCollection AddDigitalTwinForWebApi(
        this IServiceCollection services,
        string cloudConnectionString)
    {
        // ── Infrastructure: cloud only ───────────────────────────────────────
        services.AddInfrastructureCloud(cloudConnectionString);

        // Alias cloud-keyed repos → unkeyed so domain/app services resolve unchanged.
        services.AddScoped<IUserRepository>(sp =>
            sp.GetRequiredKeyedService<IUserRepository>(Cloud));
        services.AddScoped<IPatientRepository>(sp =>
            sp.GetRequiredKeyedService<IPatientRepository>(Cloud));
        services.AddScoped<IUserOAuthRepository>(sp =>
            sp.GetRequiredKeyedService<IUserOAuthRepository>(Cloud));
        services.AddScoped<IVitalSignRepository>(sp =>
            sp.GetRequiredKeyedService<IVitalSignRepository>(Cloud));
        services.AddScoped<ISleepSessionRepository>(sp =>
            sp.GetRequiredKeyedService<ISleepSessionRepository>(Cloud));
        services.AddScoped<IEnvironmentReadingRepository>(sp =>
            sp.GetRequiredKeyedService<IEnvironmentReadingRepository>(Cloud));
        services.AddScoped<IDoctorPatientAssignmentRepository>(sp =>
            sp.GetRequiredKeyedService<IDoctorPatientAssignmentRepository>(Cloud));

        // ── Domain services (only those required by the API) ─────────────────
        services.AddCoreDomainServices();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPatientService, PatientService>();

        // ── Application services ─────────────────────────────────────────────
        services.AddScoped<IDoctorPortalApplicationService, DoctorPortalApplicationService>();

        // ── Validators ───────────────────────────────────────────────────────
        services.AddValidation();

        return services;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  MAUI  (local + cloud, device providers, sync drainers)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Registers services for the .NET MAUI host.
    /// Uses local SQLite + optional cloud PostgreSQL, device-specific providers,
    /// and table drainers for local → cloud sync.
    /// <para>
    /// Pass platform-specific integrations (HealthKit, OAuth, weather, etc.)
    /// via <paramref name="registerIntegrations"/>. Composition does not reference
    /// the Integrations project so the callback keeps the boundary clean.
    /// </para>
    /// </summary>
    public static IServiceCollection AddDigitalTwinForMaui(
        this IServiceCollection services,
        string? localConnectionString = null,
        string? cloudConnectionString = null,
        Action<IServiceCollection>? registerIntegrations = null)
    {
        // ── Infrastructure ───────────────────────────────────────────────────
        services.AddInfrastructureLocal(localConnectionString);

        if (cloudConnectionString is not null)
            services.AddInfrastructureCloud(cloudConnectionString);

        // ── Domain services ──────────────────────────────────────────────────
        services.AddCoreDomainServices();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IAuthService, AuthService>();

        // ── Application services ─────────────────────────────────────────────
        services.AddScoped<IVitalsApplicationService, VitalsApplicationService>();
        services.AddScoped<IEnvironmentApplicationService, EnvironmentApplicationService>();
        services.AddScoped<IAuthApplicationService, AuthApplicationService>();
        services.AddSingleton<IHealthDataSyncService, HealthDataSyncService>();
        services.AddScoped<IMedicationApplicationService, MedicationApplicationService>();
        services.AddScoped<IDoctorPortalApplicationService, DoctorPortalApplicationService>();

        // ── Table drainers (local → cloud sync) ─────────────────────────────
        services.AddScoped<ITableDrainer>(sp => new UserDrainer(
            sp.GetRequiredService<IUserRepository>(),
            sp.GetKeyedService<IUserRepository>(Cloud),
            sp.GetRequiredService<ILogger<UserDrainer>>()));

        services.AddScoped<ITableDrainer>(sp => new PatientDrainer(
            sp.GetRequiredService<IPatientRepository>(),
            sp.GetKeyedService<IPatientRepository>(Cloud),
            sp.GetRequiredService<IUserRepository>(),
            sp.GetKeyedService<IUserRepository>(Cloud),
            sp.GetRequiredService<ILogger<PatientDrainer>>()));

        services.AddScoped<ITableDrainer>(sp => new UserOAuthDrainer(
            sp.GetRequiredService<IUserOAuthRepository>(),
            sp.GetKeyedService<IUserOAuthRepository>(Cloud),
            sp.GetRequiredService<IUserRepository>(),
            sp.GetKeyedService<IUserRepository>(Cloud),
            sp.GetRequiredService<ILogger<UserOAuthDrainer>>()));

        services.AddScoped<ITableDrainer>(sp => new VitalSignDrainer(
            sp.GetRequiredService<IVitalSignRepository>(),
            sp.GetKeyedService<IVitalSignRepository>(Cloud),
            sp.GetRequiredService<IPatientRepository>(),
            sp.GetKeyedService<IPatientRepository>(Cloud),
            sp.GetRequiredService<ILogger<VitalSignDrainer>>()));

        services.AddScoped<ITableDrainer>(sp => new EnvironmentReadingDrainer(
            sp.GetRequiredService<IEnvironmentReadingRepository>(),
            sp.GetKeyedService<IEnvironmentReadingRepository>(Cloud),
            sp.GetRequiredService<ILogger<EnvironmentReadingDrainer>>()));

        services.AddScoped<ITableDrainer>(sp => new SleepSessionDrainer(
            sp.GetRequiredService<ISleepSessionRepository>(),
            sp.GetKeyedService<ISleepSessionRepository>(Cloud),
            sp.GetRequiredService<IPatientRepository>(),
            sp.GetKeyedService<IPatientRepository>(Cloud),
            sp.GetRequiredService<ILogger<SleepSessionDrainer>>()));

        // ── Validators ───────────────────────────────────────────────────────
        services.AddValidation();

        // ── Platform integrations (HealthKit, OAuth, weather, etc.) ──────────
        registerIntegrations?.Invoke(services);

        return services;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Shared helpers
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Domain services with no platform-specific dependencies.
    /// </summary>
    private static IServiceCollection AddCoreDomainServices(this IServiceCollection services)
    {
        services.AddScoped<IVitalSignService, VitalSignService>();
        services.AddScoped<IEnvironmentAssessmentService, EnvironmentAssessmentService>();
        services.AddScoped<IMedicationInteractionService, MedicationInteractionService>();
        return services;
    }

    /// <summary>
    /// FluentValidation validators from the Application assembly.
    /// </summary>
    private static IServiceCollection AddValidation(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<Application.Validators.VitalSignDtoValidator>();
        return services;
    }
}
