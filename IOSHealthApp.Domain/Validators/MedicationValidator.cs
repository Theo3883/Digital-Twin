using FluentValidation;
using IOSHealthApp.Domain.Models;

namespace IOSHealthApp.Domain.Validators;

public class MedicationValidator : AbstractValidator<Medication>
{
    public MedicationValidator()
    {
        RuleFor(m => m.Name).NotEmpty().MaximumLength(255);
        RuleFor(m => m.Dosage).NotEmpty().MaximumLength(100);
    }
}
