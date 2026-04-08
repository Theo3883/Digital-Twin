using FluentValidation;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Validators;

public class VitalSignValidator : AbstractValidator<VitalSign>
{
    public VitalSignValidator()
    {
        RuleFor(v => v.Type).IsInEnum();

        RuleFor(v => v.Unit).NotEmpty();

        RuleFor(v => v.Value)
            .GreaterThanOrEqualTo(20).LessThanOrEqualTo(300)
            .When(v => v.Type == VitalSignType.HeartRate);

        RuleFor(v => v.Value)
            .GreaterThanOrEqualTo(0).LessThanOrEqualTo(100)
            .When(v => v.Type == VitalSignType.SpO2);

        RuleFor(v => v.Value)
            .GreaterThanOrEqualTo(0)
            .When(v => v.Type == VitalSignType.Steps);

        RuleFor(v => v.Value)
            .GreaterThanOrEqualTo(0)
            .When(v => v.Type == VitalSignType.Calories);

        RuleFor(v => v.Value)
            .GreaterThanOrEqualTo(0)
            .When(v => v.Type == VitalSignType.ActiveEnergy);

        RuleFor(v => v.Value)
            .InclusiveBetween(0, 1440)
            .When(v => v.Type == VitalSignType.ExerciseMinutes)
            .WithMessage("Exercise minutes must be between 0 and 1440.");

        RuleFor(v => v.Value)
            .InclusiveBetween(0, 24)
            .When(v => v.Type == VitalSignType.StandHours)
            .WithMessage("Stand hours must be between 0 and 24.");

        RuleFor(v => v.Source)
            .NotEmpty()
            .WithMessage("Vital sign must have a source.");
    }
}
