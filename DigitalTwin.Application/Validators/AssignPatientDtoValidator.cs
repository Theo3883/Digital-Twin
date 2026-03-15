using FluentValidation;
using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Validators;

public class AssignPatientDtoValidator : AbstractValidator<AssignPatientDto>
{
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
