using FluentValidation;
using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Validators;

/// <summary>
/// Validates requests that assign a patient to a doctor.
/// </summary>
public class AssignPatientDtoValidator : AbstractValidator<AssignPatientDto>
{
    /// <summary>
    /// Initializes validation rules for patient assignment requests.
    /// </summary>
    public AssignPatientDtoValidator()
    {
        RuleFor(a => a.PatientEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320)
            .WithMessage("A valid patient email is required.");

        RuleFor(a => a.Notes)
            .MaximumLength(500)
            .When(a => a.Notes is not null);
    }
}
