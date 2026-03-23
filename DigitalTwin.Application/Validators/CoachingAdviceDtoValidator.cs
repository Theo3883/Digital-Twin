using FluentValidation;
using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Validators;

/// <summary>
/// Validates generated coaching advice DTOs.
/// </summary>
public class CoachingAdviceDtoValidator : AbstractValidator<CoachingAdviceDto>
{
    /// <summary>
    /// Initializes validation rules for coaching advice payloads.
    /// </summary>
    public CoachingAdviceDtoValidator()
    {
        RuleFor(c => c.Advice).NotEmpty();
    }
}
