using FluentValidation;
using IOSHealthApp.Application.DTOs;

namespace IOSHealthApp.Application.Validators;

public class CoachingAdviceDtoValidator : AbstractValidator<CoachingAdviceDto>
{
    public CoachingAdviceDtoValidator()
    {
        RuleFor(c => c.Advice).NotEmpty();
    }
}
