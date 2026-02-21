using FluentValidation;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Validators;

public class MedicationValidator : AbstractValidator<Medication>
{
    public MedicationValidator()
    {
        RuleFor(m => m.Name).NotEmpty().MaximumLength(255);
        RuleFor(m => m.Dosage).NotEmpty().MaximumLength(100);
    }
}
