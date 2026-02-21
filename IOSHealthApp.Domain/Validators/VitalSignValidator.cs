using FluentValidation;
using IOSHealthApp.Domain.Enums;
using IOSHealthApp.Domain.Models;

namespace IOSHealthApp.Domain.Validators;

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
    }
}
