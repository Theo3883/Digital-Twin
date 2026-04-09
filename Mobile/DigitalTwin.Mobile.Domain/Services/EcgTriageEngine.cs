using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Services;

public class EcgTriageEngine
{
    private readonly IEnumerable<IEcgTriageRule> _rules;

    public EcgTriageEngine(IEnumerable<IEcgTriageRule> rules)
    {
        _rules = rules;
    }

    public (TriageResult Result, CriticalAlertEvent? Alert) Evaluate(EcgFrame frame)
    {
        foreach (var rule in _rules)
        {
            var result = rule.Evaluate(frame);
            if (result == TriageResult.Critical)
            {
                var alert = new CriticalAlertEvent
                {
                    RuleName = rule.RuleName,
                    Message = BuildMessage(rule.RuleName, frame),
                    Timestamp = frame.Timestamp
                };
                return (TriageResult.Critical, alert);
            }
        }

        return (TriageResult.Pass, null);
    }

    private static string BuildMessage(string ruleName, EcgFrame frame) => ruleName switch
    {
        "SignalQuality" => "ECG signal lost or flatlined. Check electrode connection.",
        "SpO2" => $"Critical SpO2: {frame.SpO2:F1}% (below 90%). Seek immediate medical attention.",
        "HeartRateActivity" => $"Critical heart rate: {frame.HeartRate} bpm at rest.",
        _ => $"Critical alert triggered by rule: {ruleName}."
    };
}
