using System.Text.RegularExpressions;

namespace DigitalTwin.OCR.Services;

public sealed partial class MedicalHistoryExtractionService
{
    // Handles optional "Rp.:" / "Rp:" prefix, optional numbering, multi-word drug names, dosage.
    [GeneratedRegex(@"^\s*(?:Rp\.?\s*:?\s*)?(?:\d+[\.\)]\s*)?(?<name>[A-Za-zĂÂÎȘȚăâîșț][\w\s\-]*?)\s+(?<dosage>\d+\s*(?:mg|g|mcg|ml)\b)(?<rest>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex MedicationLineRegex();

    // Detects lines that start a new medication entry (numbered or Rp.: prefixed).
    [GeneratedRegex(@"^\s*(?:Rp\.?\s*:?\s*)?\d+[\.\)]", RegexOptions.IgnoreCase)]
    private static partial Regex EntryStartRegex();

    public IReadOnlyList<ExtractedHistoryItem> Extract(string? sanitizedText)
    {
        if (string.IsNullOrWhiteSpace(sanitizedText))
            return [];

        // Pre-process: join continuation lines into their parent entry.
        var rawLines = sanitizedText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var merged = MergeContinuationLines(rawLines);

        var items = new List<ExtractedHistoryItem>();

        foreach (var line in merged)
        {
            var match = MedicationLineRegex().Match(line);
            if (!match.Success)
                continue;

            var name = match.Groups["name"].Value.Trim();
            var dosage = match.Groups["dosage"].Value.Trim();
            var rest = match.Groups["rest"].Value.Trim();

            var frequency = ExtractFrequency(rest);
            var duration = ExtractDuration(rest);
            var title = $"Prescription update: {name}";
            var summary = $"{name} {dosage}, {frequency}".Trim().TrimEnd(',');

            items.Add(new ExtractedHistoryItem(
                Title: title,
                MedicationName: name,
                Dosage: dosage,
                Frequency: frequency,
                Duration: duration,
                Notes: rest,
                Summary: summary,
                Confidence: EstimateConfidence(name, dosage, frequency)));
        }

        return items;
    }

    /// <summary>
    /// Lines that do not start with a number+punctuation (or Rp.:) are treated as
    /// continuation of the previous entry and appended to it.
    /// </summary>
    private static List<string> MergeContinuationLines(string[] lines)
    {
        var merged = new List<string>();

        foreach (var line in lines)
        {
            if (EntryStartRegex().IsMatch(line) || merged.Count == 0)
            {
                merged.Add(line);
            }
            else
            {
                // Check if the line itself looks like a standalone medication (has dosage).
                if (MedicationLineRegex().IsMatch(line))
                    merged.Add(line);
                else
                    merged[^1] = $"{merged[^1]} {line}";
            }
        }

        return merged;
    }

    private static string ExtractFrequency(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("diminea"))
            return "Morning";
        if (lower.Contains("seara"))
            return "Evening";
        if (lower.Contains("zi") || lower.Contains("/zi"))
            return "Daily";
        return "As prescribed";
    }

    private static string ExtractDuration(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("lung"))
            return "Long term";
        if (lower.Contains("continuu"))
            return "Continuous";
        return "Unspecified";
    }

    private static decimal EstimateConfidence(string name, string dosage, string frequency)
    {
        decimal score = 0.55m;
        if (!string.IsNullOrWhiteSpace(name)) score += 0.20m;
        if (!string.IsNullOrWhiteSpace(dosage)) score += 0.15m;
        if (!string.IsNullOrWhiteSpace(frequency) && frequency != "As prescribed") score += 0.10m;
        return Math.Min(1.0m, score);
    }
}

public sealed record ExtractedHistoryItem(
    string Title,
    string MedicationName,
    string Dosage,
    string Frequency,
    string Duration,
    string Notes,
    string Summary,
    decimal Confidence);

