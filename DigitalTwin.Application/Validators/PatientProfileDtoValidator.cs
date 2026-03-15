using FluentValidation;
using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Validators;

public class PatientProfileDtoValidator : AbstractValidator<PatientProfileDto>
{
    private const string BloodTypePattern = @"^(A|B|AB|O)[+-]$";

    public PatientProfileDtoValidator()
    {
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
    }
}
