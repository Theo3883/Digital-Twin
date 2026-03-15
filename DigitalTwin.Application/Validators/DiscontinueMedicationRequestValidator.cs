using FluentValidation;
using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Validators;

public class DiscontinueMedicationRequestValidator : AbstractValidator<DiscontinueMedicationRequest>
{
    public DiscontinueMedicationRequestValidator()
    {
        RuleFor(r => r.Reason)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("A reason is required to discontinue a medication.");
    }
}
