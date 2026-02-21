using FluentValidation;
using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Validators;

public class CoachingAdviceDtoValidator : AbstractValidator<CoachingAdviceDto>
{
    public CoachingAdviceDtoValidator()
    {
        RuleFor(c => c.Advice).NotEmpty();
    }
}
