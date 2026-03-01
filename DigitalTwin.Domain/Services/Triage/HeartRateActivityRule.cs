using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Services.Triage;

/// <summary>
/// Critical if resting heart rate exceeds 150 bpm (tachycardia threshold at rest).
/// </summary>
public class HeartRateActivityRule : IEcgTriageRule
{
    private const int CriticalRestingHrThreshold = 150;

    public string RuleName => "HeartRateActivity";

    public TriageResult Evaluate(EcgFrame frame)
    {
        if (frame.HeartRate > CriticalRestingHrThreshold)
            return TriageResult.Critical;

        return TriageResult.Pass;
    }
}
