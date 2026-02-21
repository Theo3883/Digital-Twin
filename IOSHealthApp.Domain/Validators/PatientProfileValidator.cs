using FluentValidation;
using IOSHealthApp.Domain.Models;

namespace IOSHealthApp.Domain.Validators;

public class PatientProfileValidator : AbstractValidator<PatientProfile>
{
    public PatientProfileValidator()
    {
        RuleFor(p => p.FullName).NotEmpty().MaximumLength(200);

        RuleFor(p => p.DateOfBirth)
            .LessThan(DateTime.UtcNow)
            .WithMessage("Date of birth must be in the past.");
    }
}
