using FluentValidation;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Validators;

public class UserValidator : AbstractValidator<User>
{
    public UserValidator()
    {
        RuleFor(u => u.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(u => u.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(u => u.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(u => u.DateOfBirth)
            .LessThan(DateTime.UtcNow)
            .When(u => u.DateOfBirth.HasValue)
            .WithMessage("Date of birth must be in the past.");

        RuleFor(u => u.Phone)
            .Matches(@"^\+?[\d\s\-().]{7,20}$")
            .When(u => !string.IsNullOrWhiteSpace(u.Phone))
            .WithMessage("Phone number format is invalid.");

        RuleFor(u => u.Role).IsInEnum();
    }
}
