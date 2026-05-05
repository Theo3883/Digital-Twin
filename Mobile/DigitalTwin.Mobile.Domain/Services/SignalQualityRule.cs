using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Services;

public class SignalQualityRule : IEcgTriageRule
{
    public string RuleName => "SignalQuality";

    public TriageResult Evaluate(EcgFrame frame)
    {
        if (frame.Samples.Length == 0)
            return TriageResult.Critical;

        var first = frame.Samples[0];
        var allIdentical = frame.Samples.All(s => Math.Abs(s - first) <= double.Epsilon);
        if (allIdentical)
            return TriageResult.Critical;

        return TriageResult.Pass;
    }
}
