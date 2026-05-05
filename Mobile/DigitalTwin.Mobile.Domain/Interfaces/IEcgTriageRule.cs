using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface IEcgTriageRule
{
    string RuleName { get; }
    TriageResult Evaluate(EcgFrame frame);
}
