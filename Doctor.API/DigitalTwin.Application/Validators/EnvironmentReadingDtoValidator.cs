using FluentValidation;
using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Validators;

/// <summary>
/// Validates environment reading DTO values at the application boundary.
/// </summary>
public class EnvironmentReadingDtoValidator : AbstractValidator<EnvironmentReadingDto>
{
    /// <summary>
    /// Initializes validation rules for environment readings.
    /// </summary>
    public EnvironmentReadingDtoValidator()
    {
        RuleFor(e => e.PM25).GreaterThanOrEqualTo(0);
        RuleFor(e => e.Temperature).InclusiveBetween(-60, 60);
        RuleFor(e => e.Humidity).InclusiveBetween(0, 100);
        RuleFor(e => e.AirQuality).IsInEnum();
    }
}
