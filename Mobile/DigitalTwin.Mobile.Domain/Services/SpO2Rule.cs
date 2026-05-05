using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Services;

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
