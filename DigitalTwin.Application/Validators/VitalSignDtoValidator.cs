using FluentValidation;
using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Enums;

namespace DigitalTwin.Application.Validators;

public class VitalSignDtoValidator : AbstractValidator<VitalSignDto>
{
    public VitalSignDtoValidator()
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
            .When(v => v.Type is VitalSignType.Steps or VitalSignType.Calories);
    }
}
