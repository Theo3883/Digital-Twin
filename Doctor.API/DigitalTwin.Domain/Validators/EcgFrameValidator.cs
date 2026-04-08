using FluentValidation;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Validators;

public class EcgFrameValidator : AbstractValidator<EcgFrame>
{
    public EcgFrameValidator()
    {
        RuleFor(f => f.Samples)
            .Must(s => s.Length > 0)
            .WithMessage("ECG frame must contain at least one sample.");

        RuleFor(f => f.HeartRate)
            .InclusiveBetween(20, 300)
            .WithMessage("Heart rate must be between 20 and 300 bpm.");

        RuleFor(f => f.SpO2)
            .InclusiveBetween(0.0, 100.0)
            .WithMessage("SpO2 must be between 0 and 100%.");
    }
}
