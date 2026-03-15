using FluentValidation;
using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Validators;

public class AddMedicationDtoValidator : AbstractValidator<AddMedicationDto>
{
    public AddMedicationDtoValidator()
    {
        RuleFor(m => m.Name)
            .NotEmpty()
            .MaximumLength(255)
            .WithMessage("Medication name is required.");

        RuleFor(m => m.Dosage)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Dosage is required.");

        RuleFor(m => m.Route)
            .IsInEnum()
            .WithMessage("Medication route must be a valid value.");

        RuleFor(m => m.RxCui)
            .Matches(@"^\d{1,8}$")
            .When(m => !string.IsNullOrWhiteSpace(m.RxCui))
            .WithMessage("RxCUI must be 1-8 digits.");

        RuleFor(m => m.Frequency)
            .MaximumLength(100)
            .When(m => m.Frequency is not null);

        RuleFor(m => m.Instructions)
            .MaximumLength(500)
            .When(m => m.Instructions is not null);

        RuleFor(m => m.Reason)
            .MaximumLength(500)
            .When(m => m.Reason is not null);
    }
}
