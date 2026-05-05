using FluentValidation;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Services;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Services;
using DigitalTwin.Domain.Models;
using DigitalTwin.Domain.Services;
using DigitalTwin.Infrastructure;
using DigitalTwin.Infrastructure.Policies;
using DigitalTwin.Infrastructure.Services;
using DigitalTwin.Infrastructure.Services.Notifications;
using DigitalTwin.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Composition;

/// <summary>
/// Central DI hub for the ASP.NET Core Web API host (<c>AddDigitalTwinForWebApi</c>).
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
        services.AddScoped<IMedicationRepository>(sp =>
            sp.GetRequiredKeyedService<IMedicationRepository>(Cloud));
        services.AddScoped<IOcrDocumentRepository>(sp =>
            sp.GetRequiredKeyedService<IOcrDocumentRepository>(Cloud));
        services.AddScoped<IMedicalHistoryEntryRepository>(sp =>
            sp.GetRequiredKeyedService<IMedicalHistoryEntryRepository>(Cloud));
        services.AddScoped<INotificationRepository>(sp =>
            sp.GetRequiredKeyedService<INotificationRepository>(Cloud));

        // ── Domain services (only those required by the API) ─────────────────
        services.AddCoreDomainServices();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDoctorPortalDomainService>(sp => new DoctorPortalDomainService(
            new DoctorPortalDomainService.Repositories(
                sp.GetRequiredService<IUserRepository>(),
                sp.GetRequiredService<IPatientRepository>(),
                sp.GetRequiredService<IDoctorPatientAssignmentRepository>(),
                sp.GetRequiredService<IVitalSignRepository>(),
                sp.GetRequiredService<ISleepSessionRepository>(),
                sp.GetRequiredService<IMedicationRepository>()),
            sp.GetRequiredService<IMedicationService>(),
            sp.GetRequiredService<IDomainEventDispatcher>()));
        services.AddScoped<IDoctorPatientAssignmentService>(sp => new DoctorPatientAssignmentService(
            sp.GetRequiredService<IDoctorPatientAssignmentRepository>(),
            sp.GetRequiredService<IUserRepository>()));

        // ── Infrastructure services ──────────────────────────────────────────
        services.AddSingleton<ITransientFailurePolicy, NpgsqlTransientFailurePolicy>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IDomainEventHandler<PatientAssignedEvent>, NotificationEventHandlers>();
        services.AddScoped<IDomainEventHandler<PatientUnassignedEvent>, NotificationEventHandlers>();
        services.AddScoped<IDomainEventHandler<MedicationAddedEvent>, NotificationEventHandlers>();
        services.AddScoped<IDomainEventHandler<MedicationDiscontinuedEvent>, NotificationEventHandlers>();
        services.AddScoped<IDomainEventHandler<MedicationDeletedEvent>, NotificationEventHandlers>();
        services.AddScoped<IMedicationManagementService>(sp => new MedicationManagementService(
            sp.GetRequiredService<IMedicationRepository>(),
            sp.GetKeyedService<IMedicationRepository>(Cloud),
            sp.GetRequiredService<IMedicationService>(),
            sp.GetRequiredService<ILogger<MedicationManagementService>>()));
        services.AddScoped<IPersistenceGateway<EnvironmentReading>>(sp =>
            new EnvironmentReadingPersistenceGateway(
                sp.GetRequiredService<IEnvironmentReadingRepository>(),
                sp.GetKeyedService<IEnvironmentReadingRepository>(Cloud),
                sp.GetRequiredService<ILogger<EnvironmentReadingPersistenceGateway>>()));

        // ── Application services ─────────────────────────────────────────────
        services.AddScoped<IDoctorPortalApplicationService>(sp => new DoctorPortalApplicationService(
            sp.GetRequiredService<IDoctorPortalDomainService>(),
            sp.GetRequiredService<IRxCuiLookupProvider>(),
            sp.GetRequiredService<IMedicalHistoryEntryRepository>(),
            sp.GetRequiredService<IMedicationInteractionProvider>(),
            sp.GetRequiredService<AppDebugLogger<DoctorPortalApplicationService>>()));
        services.AddScoped<IDoctorAssignmentApplicationService>(sp => new DoctorAssignmentApplicationService(
            sp.GetRequiredService<IDoctorPatientAssignmentService>(),
            sp.GetRequiredService<ILogger<DoctorAssignmentApplicationService>>()));

        // ── Validators ───────────────────────────────────────────────────────
        services.AddValidation();

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
        // ── Shared debug logger wrapper (guards expensive arg evaluation) ────
        services.AddSingleton(typeof(AppDebugLogger<>));

        services.AddScoped<IMedicationInteractionService, MedicationInteractionService>();
        services.AddScoped<IMedicationService, MedicationService>();

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
