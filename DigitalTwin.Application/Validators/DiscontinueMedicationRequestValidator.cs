using FluentValidation;
using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Validators;

/// <summary>
/// Validates requests that discontinue medications.
/// </summary>
public class DiscontinueMedicationRequestValidator : AbstractValidator<DiscontinueMedicationRequest>
{
    /// <summary>
    /// Initializes validation rules for medication discontinuation requests.
    /// </summary>
    public DiscontinueMedicationRequestValidator()
    {
        RuleFor(r => r.Reason)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("A reason is required to discontinue a medication.");
    }
}
