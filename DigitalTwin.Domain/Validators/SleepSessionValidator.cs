using FluentValidation;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Validators;

public class SleepSessionValidator : AbstractValidator<SleepSession>
{
    public SleepSessionValidator()
    {
        RuleFor(s => s.PatientId)
            .NotEmpty();

        RuleFor(s => s.StartTime)
            .LessThan(s => s.EndTime)
            .WithMessage("Sleep session start time must be before end time.");

        RuleFor(s => s.EndTime)
            .GreaterThan(s => s.StartTime)
            .WithMessage("Sleep session end time must be after start time.");

        RuleFor(s => s.DurationMinutes)
            .GreaterThan(0)
            .LessThanOrEqualTo(1440)
            .WithMessage("Duration must be between 1 and 1440 minutes (24 hours).");

        RuleFor(s => s.QualityScore)
            .InclusiveBetween(0, 100)
            .WithMessage("Quality score must be between 0 and 100.");
    }
}
