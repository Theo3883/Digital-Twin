using FluentValidation;
using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Enums;

namespace DigitalTwin.Application.Validators;

/// <summary>
/// Validates the application-layer structure of vital-sign DTOs.
/// </summary>
public class VitalSignDtoValidator : AbstractValidator<VitalSignDto>
{
    /// <summary>
    /// Initializes validation rules for vital-sign DTOs.
    /// </summary>
    public VitalSignDtoValidator()
    {
        RuleFor(v => v.Type).IsInEnum();
        RuleFor(v => v.Unit).NotEmpty();
    }
}
