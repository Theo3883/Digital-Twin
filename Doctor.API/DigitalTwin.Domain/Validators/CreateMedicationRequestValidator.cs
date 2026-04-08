using FluentValidation;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Validators;

public class CreateMedicationRequestValidator : AbstractValidator<CreateMedicationRequest>
{
    public CreateMedicationRequestValidator()
    {
        RuleFor(r => r.PatientId)
            .NotEmpty()
            .WithMessage("PatientId is required.");

        RuleFor(r => r.Name)
            .NotEmpty()
            .MaximumLength(255)
            .WithMessage("Medication name is required and must not exceed 255 characters.");

        RuleFor(r => r.Dosage)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Dosage is required and must not exceed 100 characters.");

        RuleFor(r => r.Route)
            .IsInEnum()
            .WithMessage("Medication route must be a valid value.");

        RuleFor(r => r.RxCui)
            .Matches(@"^\d{1,8}$")
            .When(r => !string.IsNullOrWhiteSpace(r.RxCui))
            .WithMessage("RxCUI must be 1-8 digits.");

        RuleFor(r => r.Frequency)
            .MaximumLength(100)
            .When(r => r.Frequency is not null);

        RuleFor(r => r.AddedByRole)
            .IsInEnum();
    }
}
