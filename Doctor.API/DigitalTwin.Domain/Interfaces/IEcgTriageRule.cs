using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface IEcgTriageRule
{
    string RuleName { get; }

    TriageResult Evaluate(EcgFrame frame);
}
