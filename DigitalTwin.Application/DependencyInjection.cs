using FluentValidation;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Services;
using DigitalTwin.Application.Sync.Drainers;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using DigitalTwin.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // ── Domain services ──────────────────────────────────────────────────────
        services.AddScoped<IVitalSignService, VitalSignService>();
        services.AddScoped<IEnvironmentAssessmentService, EnvironmentAssessmentService>();
        services.AddScoped<IMedicationInteractionService, MedicationInteractionService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IAuthService, AuthService>();

        // ── Application services ─────────────────────────────────────────────────
        services.AddScoped<IVitalsApplicationService, VitalsApplicationService>();
        services.AddScoped<IEnvironmentApplicationService, EnvironmentApplicationService>();
        services.AddScoped<IAuthApplicationService, AuthApplicationService>();
        services.AddSingleton<IHealthDataSyncService, HealthDataSyncService>();
        services.AddScoped<IMedicationApplicationService, MedicationApplicationService>();

        // ── Table drainers ───────────────────────────────────────────────────────
        // Each drainer is registered as ITableDrainer. HealthDataSyncService resolves
        // them all via IEnumerable<ITableDrainer> and calls DrainAsync() in order.
        // To sync a new entity: add one class + one line here. Nothing else changes.

        services.AddScoped<ITableDrainer>(sp => new UserDrainer(
            sp.GetRequiredService<IUserRepository>(),
            sp.GetKeyedService<IUserRepository>("Cloud"),
            sp.GetRequiredService<ILogger<UserDrainer>>()));

        services.AddScoped<ITableDrainer>(sp => new PatientDrainer(
            sp.GetRequiredService<IPatientRepository>(),
            sp.GetKeyedService<IPatientRepository>("Cloud"),
            sp.GetRequiredService<IUserRepository>(),
            sp.GetKeyedService<IUserRepository>("Cloud"),
            sp.GetRequiredService<ILogger<PatientDrainer>>()));

        services.AddScoped<ITableDrainer>(sp => new UserOAuthDrainer(
            sp.GetRequiredService<IUserOAuthRepository>(),
            sp.GetKeyedService<IUserOAuthRepository>("Cloud"),
            sp.GetRequiredService<IUserRepository>(),
            sp.GetKeyedService<IUserRepository>("Cloud"),
            sp.GetRequiredService<ILogger<UserOAuthDrainer>>()));

        services.AddScoped<ITableDrainer>(sp => new VitalSignDrainer(
            sp.GetRequiredService<IVitalSignRepository>(),
            sp.GetKeyedService<IVitalSignRepository>("Cloud"),
            sp.GetRequiredService<IPatientRepository>(),
            sp.GetKeyedService<IPatientRepository>("Cloud"),
            sp.GetRequiredService<ILogger<VitalSignDrainer>>()));

        services.AddScoped<ITableDrainer>(sp => new EnvironmentReadingDrainer(
            sp.GetRequiredService<IEnvironmentReadingRepository>(),
            sp.GetKeyedService<IEnvironmentReadingRepository>("Cloud"),
            sp.GetRequiredService<ILogger<EnvironmentReadingDrainer>>()));

        // ── Validators ───────────────────────────────────────────────────────────
        services.AddValidatorsFromAssemblyContaining<Validators.VitalSignDtoValidator>();

        return services;
    }
}
