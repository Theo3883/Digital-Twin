using FluentValidation;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Validators;

public class DoctorPatientAssignmentValidator : AbstractValidator<DoctorPatientAssignment>
{
    public DoctorPatientAssignmentValidator()
    {
        RuleFor(a => a.DoctorId)
            .NotEmpty()
            .WithMessage("DoctorId is required.");

        RuleFor(a => a.PatientId)
            .NotEmpty()
            .WithMessage("PatientId is required.");

        RuleFor(a => a.PatientEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320)
            .WithMessage("A valid patient email is required.");

        RuleFor(a => a.AssignedByDoctorId)
            .NotEmpty()
            .WithMessage("AssignedByDoctorId is required.");

        RuleFor(a => a.Notes)
            .MaximumLength(500)
            .When(a => a.Notes is not null);
    }
}
