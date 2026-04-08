using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Services.Triage;

/// <summary>
/// Critical if SpO2 drops below 90% (clinically significant hypoxemia).
/// </summary>
public class SpO2Rule : IEcgTriageRule
{
    private const double CriticalThreshold = 90.0;

    public string RuleName => "SpO2";

    public TriageResult Evaluate(EcgFrame frame)
    {
        if (frame.SpO2 > 0 && frame.SpO2 < CriticalThreshold)
            return TriageResult.Critical;

        return TriageResult.Pass;
    }
}
