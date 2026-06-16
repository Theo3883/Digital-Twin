using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Services;

public class EcgTriageEngine
{
    private const string NormalLabel = "Normal";
    private const double MinConfidenceThreshold = 0.60;
    private const double HighConfidenceThreshold = 0.80;

    private static readonly Dictionary<string, string> MlDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AFib"]        = "Atrial fibrillation",
        ["Bradycardia"] = "Bradycardia",
        ["LongQT"]      = "Long QT interval",
        ["PVC"]         = "Premature ventricular complex",
        ["STEMI"]       = "ST-elevation myocardial infarction",
        ["Tachycardia"] = "Tachycardia"
    };

    /// <summary>
    /// Labels that are always Critical regardless of confidence (life-threatening).
    /// </summary>
    private static readonly HashSet<string> AlwaysCriticalLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "STEMI", "AFib"
    };

    private readonly IEnumerable<IEcgTriageRule> _rules;

    public EcgTriageEngine(IEnumerable<IEcgTriageRule> rules)
    {
        _rules = rules;
    }

    public (TriageResult Result, CriticalAlertEvent? Alert) Evaluate(EcgFrame frame)
    {
        // ── 1. Domain rules (signal quality, SpO2, extreme heart rate) ───────
        // Collect the worst result across all rules, not just the first Critical.
        var worstResult = TriageResult.Pass;
        IEcgTriageRule? worstRule = null;

        foreach (var rule in _rules)
        {
            var result = rule.Evaluate(frame);
            if (result > worstResult)
            {
                worstResult = result;
                worstRule = rule;
            }
        }

        if (worstResult == TriageResult.Critical && worstRule != null)
        {
            return (TriageResult.Critical, new CriticalAlertEvent
            {
                RuleName  = worstRule.RuleName,
                Message   = BuildMessage(worstRule.RuleName, frame),
                Timestamp = frame.Timestamp
            });
        }

        // ── 2. ML-driven detection (XceptionTime ONNX scores) ─────────────────
        if (frame.MlScores is { Count: > 0 })
        {
            var top = frame.MlScores
                .OrderByDescending(kv => kv.Value)
                .Select(kv => (Label: kv.Key, Prob: kv.Value))
                .FirstOrDefault();

            // Ignore the model below the minimum confidence floor.
            if (!string.IsNullOrWhiteSpace(top.Label)
                && !IsNormalLabel(top.Label)
                && top.Prob >= MinConfidenceThreshold)
            {
                var mlSeverity = ClassifyMlSeverity(top.Label, top.Prob);

                if (mlSeverity == TriageResult.Critical)
                {
                    return (TriageResult.Critical, new CriticalAlertEvent
                    {
                        RuleName  = $"ML_{top.Label}",
                        Message   = BuildMlMessage(top.Label, top.Prob),
                        Timestamp = frame.Timestamp
                    });
                }

                if (mlSeverity == TriageResult.Warn)
                {
                    // Return Warn with an alert so the UI shows the prediction,
                    // but does NOT fire an emergency notification.
                    return (TriageResult.Warn, new CriticalAlertEvent
                    {
                        RuleName  = $"ML_{top.Label}",
                        Message   = BuildMlMessage(top.Label, top.Prob),
                        Timestamp = frame.Timestamp
                    });
                }
            }
        }

        // ── 3. Propagate domain Warn if no ML finding overrode it ─────────────
        if (worstResult == TriageResult.Warn && worstRule != null)
        {
            return (TriageResult.Warn, new CriticalAlertEvent
            {
                RuleName  = worstRule.RuleName,
                Message   = BuildMessage(worstRule.RuleName, frame),
                Timestamp = frame.Timestamp
            });
        }

        return (TriageResult.Pass, null);
    }

    /// <summary>
    /// Maps a model label + confidence to a TriageResult severity.
    /// </summary>
    private static TriageResult ClassifyMlSeverity(string label, double prob)
    {
        // Always-critical labels (STEMI, AFib) regardless of confidence.
        if (AlwaysCriticalLabels.Contains(label))
            return TriageResult.Critical;

        // Other abnormal labels: Critical at high confidence, Warn otherwise.
        return prob >= HighConfidenceThreshold
            ? TriageResult.Critical
            : TriageResult.Warn;
    }

    private static string BuildMessage(string ruleName, EcgFrame frame) => ruleName switch
    {
        "SignalQuality"     => "ECG signal lost or flatlined. Check electrode connection.",
        "SpO2"             => $"Critical SpO2: {frame.SpO2:F1}% (below 90%). Seek immediate medical attention.",
        "HeartRateActivity" => $"Tachycardia: {frame.HeartRate} bpm (above 150 bpm) at rest.",
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
