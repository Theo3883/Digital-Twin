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

        services.AddScoped<IVitalsApplicationService, VitalsApplicationService>();
        services.AddScoped<IEnvironmentApplicationService, EnvironmentApplicationService>();

        services.AddValidatorsFromAssemblyContaining<Validators.VitalSignDtoValidator>();

        return services;
    }
}
