using FluentValidation;
using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Enums;

namespace DigitalTwin.Application.Validators;

/// <summary>
/// Validates DTO structure. Value range invariants are enforced by
/// <c>DigitalTwin.Domain.Validators.VitalSignValidator</c> on the domain model.
/// </summary>
public class VitalSignDtoValidator : AbstractValidator<VitalSignDto>
{
    public VitalSignDtoValidator()
    {
        RuleFor(v => v.Type).IsInEnum();
        RuleFor(v => v.Unit).NotEmpty();
    }
}
