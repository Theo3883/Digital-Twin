using FluentValidation;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Services;
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

        services.AddScoped<IVitalsApplicationService, VitalsApplicationService>();
        services.AddScoped<IEnvironmentApplicationService, EnvironmentApplicationService>();
        services.AddScoped<IAuthApplicationService, AuthApplicationService>();
        services.AddScoped<IHealthDataSyncService, HealthDataSyncService>();
        services.AddScoped<IMedicationApplicationService, MedicationApplicationService>();

        services.AddValidatorsFromAssemblyContaining<Validators.VitalSignDtoValidator>();

        return services;
    }
}
