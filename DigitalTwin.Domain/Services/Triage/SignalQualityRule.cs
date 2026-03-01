using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Services.Triage;

/// <summary>
/// Fails if the ECG signal is flatlined (all samples identical) or if the frame contains no samples.
/// </summary>
public class SignalQualityRule : IEcgTriageRule
{
    public string RuleName => "SignalQuality";

    public TriageResult Evaluate(EcgFrame frame)
    {
        if (frame.Samples.Length == 0)
            return TriageResult.Critical;

        var first = frame.Samples[0];
        var allIdentical = frame.Samples.All(s => s == first);
        if (allIdentical)
            return TriageResult.Critical;

        return TriageResult.Pass;
    }
}
