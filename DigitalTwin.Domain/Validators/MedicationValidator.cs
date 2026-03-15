using FluentValidation;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Validators;

public class MedicationValidator : AbstractValidator<Medication>
{
    public MedicationValidator()
    {
        RuleFor(m => m.Name).NotEmpty().MaximumLength(255);
        RuleFor(m => m.Dosage).NotEmpty().MaximumLength(100);
        RuleFor(m => m.PatientId).NotEmpty();
        RuleFor(m => m.Status).IsInEnum();
        RuleFor(m => m.Route).IsInEnum();
        RuleFor(m => m.AddedByRole).IsInEnum();

        RuleFor(m => m.RxCui)
            .Matches(@"^\d{1,8}$")
            .When(m => !string.IsNullOrWhiteSpace(m.RxCui))
            .WithMessage("RxCUI must be 1-8 digits.");

        RuleFor(m => m.Frequency).MaximumLength(100).When(m => m.Frequency is not null);
        RuleFor(m => m.Instructions).MaximumLength(500).When(m => m.Instructions is not null);
        RuleFor(m => m.Reason).MaximumLength(500).When(m => m.Reason is not null);
    }
}
