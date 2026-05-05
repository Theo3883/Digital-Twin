using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Services;

public class EcgTriageEngine
{
    private const string NormalLabel = "Normal";

    private static readonly Dictionary<string, string> MlDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AFib"] = "Atrial fibrillation",
        ["Bradycardia"] = "Bradycardia",
        ["LongQT"] = "Long QT interval",
        ["PVC"] = "Premature ventricular complex",
        ["STEMI"] = "ST-elevation myocardial infarction",
        ["Tachycardia"] = "Tachycardia"
    };

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

        // ── 2. ML-driven detection (XceptionTime ONNX scores) ────────────────
        if (frame.MlScores is { Count: > 0 })
        {
            var topPrediction = frame.MlScores
                .OrderByDescending(kv => kv.Value)
                .Select(kv => (Label: kv.Key, Prob: kv.Value))
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(topPrediction.Label)
                && !IsNormalLabel(topPrediction.Label))
            {
                var alert = new CriticalAlertEvent
                {
                    RuleName = $"ML_{topPrediction.Label}",
                    Message = BuildMlMessage(topPrediction.Label, topPrediction.Prob),
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

    private static string BuildMlMessage(string label, double prob)
    {
        var displayName = MlDisplayNames.GetValueOrDefault(label, label);
        return $"{displayName} detected by XceptionTime (PTB-XL) ({prob:P0} confidence). Seek medical evaluation.";
    }

    private static bool IsNormalLabel(string label)
        => label.Equals(NormalLabel, StringComparison.OrdinalIgnoreCase);
}

