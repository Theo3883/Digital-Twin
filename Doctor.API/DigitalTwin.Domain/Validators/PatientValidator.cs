using FluentValidation;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Validators;

public class PatientValidator : AbstractValidator<Patient>
{
    // ABO + Rh blood type codes
    private const string BloodTypePattern = @"^(A|B|AB|O)[+-]$";

    public PatientValidator()
    {
        RuleFor(p => p.UserId)
            .NotEmpty()
            .WithMessage("Patient must be linked to a user.");

        RuleFor(p => p.BloodType)
            .Matches(BloodTypePattern)
            .When(p => !string.IsNullOrWhiteSpace(p.BloodType))
            .WithMessage("Blood type must be one of: A+, A-, B+, B-, AB+, AB-, O+, O-.");

        RuleFor(p => p.Allergies)
            .MaximumLength(1000)
            .When(p => p.Allergies is not null);

        RuleFor(p => p.MedicalHistoryNotes)
            .MaximumLength(5000)
            .When(p => p.MedicalHistoryNotes is not null);

        RuleFor(p => p.Cnp)
            .NotEmpty()
            .WithMessage("CNP is required.");

        RuleFor(p => p.Cnp)
            .Must(CnpValidator.IsValidFormat)
            .WithMessage("CNP must be exactly 13 digits and start with 1–6.")
            .When(p => !string.IsNullOrWhiteSpace(p.Cnp));

        RuleFor(p => p.Cnp)
            .Must(cnp => CnpValidator.IsValidDate(cnp!))
            .WithMessage("CNP contains an invalid date of birth.")
            .When(p => !string.IsNullOrWhiteSpace(p.Cnp) && CnpValidator.IsValidFormat(p.Cnp!));

        RuleFor(p => p.Cnp)
            .Must(cnp => CnpValidator.IsValidCountyCode(cnp!))
            .WithMessage("CNP contains an invalid county code.")
            .When(p => !string.IsNullOrWhiteSpace(p.Cnp) && CnpValidator.IsValidFormat(p.Cnp!));

        RuleFor(p => p.Cnp)
            .Must(cnp => CnpValidator.IsValidChecksum(cnp!))
            .WithMessage("CNP has an invalid checksum digit.")
            .When(p => !string.IsNullOrWhiteSpace(p.Cnp) && CnpValidator.IsValidFormat(p.Cnp!));
    }
}
