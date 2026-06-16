using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Services;

public class HeartRateActivityRule : IEcgTriageRule
{
    // Safety-net only: catches extreme tachycardia when the ML model has no buffer yet.
    // Bradycardia detection is owned by the XceptionTime ONNX model ("Bradycardia" label).
    private const int CriticalHighHrThreshold = 150;

    public string RuleName => "HeartRateActivity";

    public TriageResult Evaluate(EcgFrame frame)
    {
        if (frame.HeartRate > CriticalHighHrThreshold)
            return TriageResult.Critical;

        return TriageResult.Pass;
    }
}
