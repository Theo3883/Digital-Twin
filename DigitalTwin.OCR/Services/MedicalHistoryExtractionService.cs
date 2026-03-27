using System.Text.RegularExpressions;

namespace DigitalTwin.OCR.Services;

public sealed class MedicalHistoryExtractionService
{
    private static readonly Regex MedicationLineRegex = new(
        @"^\s*(?:\d+[\.\)]\s*)?(?<name>[A-Za-zĂÂÎȘȚăâîșț\-]+)\s+(?<dosage>\d+\s*(?:mg|g|mcg|ml))(?<rest>.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IReadOnlyList<ExtractedHistoryItem> Extract(string? sanitizedText)
    {
        if (string.IsNullOrWhiteSpace(sanitizedText))
            return [];

        var lines = sanitizedText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var items = new List<ExtractedHistoryItem>();

        foreach (var line in lines)
        {
            var match = MedicationLineRegex.Match(line);
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

