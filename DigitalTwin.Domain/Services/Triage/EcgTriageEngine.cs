using System.Reactive.Subjects;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Events;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Services.Triage;

public class EcgTriageEngine
{
    private readonly IEnumerable<IEcgTriageRule> _rules;
    private readonly Subject<CriticalAlertEvent> _criticalAlerts = new();

    public IObservable<CriticalAlertEvent> CriticalAlerts => _criticalAlerts;

    public EcgTriageEngine(IEnumerable<IEcgTriageRule> rules)
    {
        _rules = rules;
    }

    /// <summary>
    /// Evaluates all rules in order. Stops at the first Critical result and raises a
    /// <see cref="CriticalAlertEvent"/>. Returns the overall worst result.
    /// </summary>
    public TriageResult Evaluate(EcgFrame frame)
    {
        foreach (var rule in _rules)
        {
            var result = rule.Evaluate(frame);
            if (result == TriageResult.Critical)
            {
                _criticalAlerts.OnNext(new CriticalAlertEvent
                {
                    RuleName = rule.RuleName,
                    Message = BuildMessage(rule.RuleName, frame),
                    Timestamp = frame.Timestamp
                });
                return TriageResult.Critical;
            }
        }

        return TriageResult.Pass;
    }

    private static string BuildMessage(string ruleName, EcgFrame frame) => ruleName switch
    {
        "SignalQuality" => "ECG signal lost or flatlined. Check electrode connection.",
        "SpO2" => $"Critical SpO2: {frame.SpO2:F1}% (below 90%). Seek immediate medical attention.",
        "HeartRateActivity" => $"Critical heart rate: {frame.HeartRate} bpm at rest.",
        _ => $"Critical alert triggered by rule: {ruleName}."
    };
}
