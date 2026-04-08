using FluentValidation;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Validators;

public class EnvironmentReadingValidator : AbstractValidator<EnvironmentReading>
{
    public EnvironmentReadingValidator()
    {
        RuleFor(e => e.PM25).GreaterThanOrEqualTo(0);

        RuleFor(e => e.Temperature)
            .InclusiveBetween(-60, 60)
            .WithMessage("Temperature must be between -60°C and 60°C.");

        RuleFor(e => e.Humidity)
            .InclusiveBetween(0, 100);

        RuleFor(e => e.AirQuality).IsInEnum();
    }
}
