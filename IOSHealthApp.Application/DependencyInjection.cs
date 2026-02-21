using FluentValidation;
using IOSHealthApp.Application.Interfaces;
using IOSHealthApp.Application.Services;
using IOSHealthApp.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IOSHealthApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<VitalSignService>();
        services.AddScoped<EnvironmentAssessmentService>();

        services.AddScoped<IVitalsApplicationService, VitalsApplicationService>();
        services.AddScoped<IEnvironmentApplicationService, EnvironmentApplicationService>();

        services.AddValidatorsFromAssemblyContaining<Validators.VitalSignDtoValidator>();

        return services;
    }
}
