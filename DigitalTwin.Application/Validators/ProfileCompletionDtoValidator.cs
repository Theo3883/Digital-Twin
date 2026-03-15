using FluentValidation;
using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Validators;

public class ProfileCompletionDtoValidator : AbstractValidator<ProfileCompletionDto>
{
    public ProfileCompletionDtoValidator()
    {
        RuleFor(p => p.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("First name is required.");

        RuleFor(p => p.LastName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Last name is required.");

        RuleFor(p => p.DateOfBirth)
            .LessThan(DateTime.UtcNow)
            .When(p => p.DateOfBirth.HasValue)
            .WithMessage("Date of birth must be in the past.");

        RuleFor(p => p.Phone)
            .Matches(@"^\+?[\d\s\-().]{7,20}$")
            .When(p => !string.IsNullOrWhiteSpace(p.Phone))
            .WithMessage("Phone number format is invalid.");

        RuleFor(p => p.Address)
            .MaximumLength(255)
            .When(p => p.Address is not null);

        RuleFor(p => p.City)
            .MaximumLength(100)
            .When(p => p.City is not null);

        RuleFor(p => p.Country)
            .MaximumLength(100)
            .When(p => p.Country is not null);
    }
}
