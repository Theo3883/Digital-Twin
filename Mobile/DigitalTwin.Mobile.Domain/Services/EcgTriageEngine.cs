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
        // ── 1. Domain rules (signal quality, SpO2, heart rate) ───────────────
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

        // ── 2. ML-driven detection (CoreML ECGClassifier scores) ─────────────
        if (frame.MlScores is { Count: > 0 })
        {
            // Find highest-confidence abnormality above threshold
            const double threshold = 0.5;
            var topAbnormality = frame.MlScores
                .Where(kv => kv.Value > threshold)
                .OrderByDescending(kv => kv.Value)
                .Select(kv => (Label: kv.Key, Prob: kv.Value))
                .FirstOrDefault();

            if (topAbnormality.Label is not null)
            {
                var alert = new CriticalAlertEvent
                {
                    RuleName = $"ML_{topAbnormality.Label}",
                    Message = BuildMlMessage(topAbnormality.Label, topAbnormality.Prob),
                    Timestamp = frame.Timestamp
                };
                return (TriageResult.Critical, alert);
            }
        }

        return (TriageResult.Pass, null);
    }

    private static string BuildMessage(string ruleName, EcgFrame frame) => ruleName switch
    {
        "SignalQuality"     => "ECG signal lost or flatlined. Check electrode connection.",
        "SpO2"             => $"Critical SpO2: {frame.SpO2:F1}% (below 90%). Seek immediate medical attention.",
        "HeartRateActivity" => $"Critical heart rate: {frame.HeartRate} bpm at rest.",
        _                  => $"Critical alert triggered by rule: {ruleName}."
    };

    private static string BuildMlMessage(string label, double prob) => label switch
    {
        "AF"    => $"Atrial Fibrillation detected by ResNet CNN ({prob:P0} confidence). Seek medical evaluation.",
        "RBBB"  => $"Right Bundle Branch Block detected by CNN ({prob:P0} confidence).",
        "LBBB"  => $"Left Bundle Branch Block detected by CNN ({prob:P0} confidence). May indicate cardiac disease.",
        "SB"    => $"Sinus Bradycardia confirmed by CNN ({prob:P0} confidence). Heart rate critically low.",
        "ST"    => $"Sinus Tachycardia confirmed by CNN ({prob:P0} confidence). Heart rate critically elevated.",
        "1dAVb" => $"1st Degree AV Block detected ({prob:P0} confidence). Conduction delay detected.",
        _       => $"Cardiac abnormality '{label}' detected ({prob:P0} confidence)."
    };
}

