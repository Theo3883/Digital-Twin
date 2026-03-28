using FluentValidation;
using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Validators;

/// <summary>
/// Validates patient medical profile input.
/// </summary>
public class PatientProfileDtoValidator : AbstractValidator<PatientProfileDto>
{
    private const string BloodTypePattern = @"^(A|B|AB|O)[+-]$";

    /// <summary>
    /// Initializes validation rules for patient profiles.
    /// </summary>
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

        RuleFor(p => p.Weight)
            .GreaterThan(0)
            .LessThanOrEqualTo(500)
            .When(p => p.Weight.HasValue)
            .WithMessage("Weight must be between 0 and 500.");

        RuleFor(p => p.Height)
            .GreaterThan(0)
            .LessThanOrEqualTo(300)
            .When(p => p.Height.HasValue)
            .WithMessage("Height must be between 0 and 300.");

        RuleFor(p => p.Cholesterol)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(30)
            .When(p => p.Cholesterol.HasValue)
            .WithMessage("Cholesterol must be between 0 and 30.");

        RuleFor(p => p.BloodPressureSystolic)
            .InclusiveBetween(50, 260)
            .When(p => p.BloodPressureSystolic.HasValue)
            .WithMessage("Blood pressure systolic must be between 50 and 260.");

        RuleFor(p => p.BloodPressureDiastolic)
            .InclusiveBetween(30, 180)
            .When(p => p.BloodPressureDiastolic.HasValue)
            .WithMessage("Blood pressure diastolic must be between 30 and 180.");

        RuleFor(p => p)
            .Must(p =>
                !(p.BloodPressureSystolic.HasValue ^ p.BloodPressureDiastolic.HasValue))
            .WithMessage("Blood pressure must include both systolic and diastolic values (or neither).");
    }
}
