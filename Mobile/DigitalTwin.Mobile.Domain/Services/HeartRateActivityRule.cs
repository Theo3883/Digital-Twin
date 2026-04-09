using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Services;

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
