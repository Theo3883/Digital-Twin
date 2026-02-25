using FluentValidation;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Services;
using DigitalTwin.Application.Sync;
using DigitalTwin.Domain.Models;
using DigitalTwin.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalTwin.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<VitalSignService>();
        services.AddScoped<EnvironmentAssessmentService>();
        services.AddScoped<MedicationInteractionService>();

        services.AddScoped<ILocalSyncStore<VitalSign>, VitalSignLocalSyncStore>();
        services.AddScoped<ICloudSyncStore<VitalSign>, VitalSignCloudSyncStore>();
        services.AddScoped<ISyncFacade<VitalSign>, SyncFacade<VitalSign>>();

        services.AddScoped<ILocalSyncStore<User>, UserLocalSyncStore>();
        services.AddScoped<ICloudSyncStore<User>, UserCloudSyncStore>();
        services.AddScoped<ISyncFacade<User>, SyncFacade<User>>();

        services.AddScoped<ILocalSyncStore<Patient>, PatientLocalSyncStore>();
        services.AddScoped<ICloudSyncStore<Patient>, PatientCloudSyncStore>();
        services.AddScoped<ISyncFacade<Patient>, SyncFacade<Patient>>();

        services.AddScoped<ILocalSyncStore<UserOAuth>, UserOAuthLocalSyncStore>();
        services.AddScoped<ICloudSyncStore<UserOAuth>, UserOAuthCloudSyncStore>();
        services.AddScoped<ISyncFacade<UserOAuth>, SyncFacade<UserOAuth>>();

        services.AddScoped<IVitalsApplicationService, VitalsApplicationService>();
        services.AddScoped<IEnvironmentApplicationService, EnvironmentApplicationService>();
        services.AddScoped<IAuthApplicationService, AuthApplicationService>();
        services.AddSingleton<IHealthDataSyncService, HealthDataSyncService>();
        services.AddScoped<IMedicationApplicationService, MedicationApplicationService>();

        services.AddValidatorsFromAssemblyContaining<Validators.VitalSignDtoValidator>();

        return services;
    }
}
