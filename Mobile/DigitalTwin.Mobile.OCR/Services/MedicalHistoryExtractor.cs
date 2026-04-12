using System.Text.RegularExpressions;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.OCR.Services;

/// <summary>
/// Extracts medical history items from sanitized OCR text.
/// For Prescriptions: extracts individual medication lines.
/// For other doc types: creates a document-level history entry from extracted fields.
/// </summary>
public sealed partial class MedicalHistoryExtractor : IMedicalHistoryExtractor
{
    private readonly ILogger<MedicalHistoryExtractor> _logger;

    public MedicalHistoryExtractor(ILogger<MedicalHistoryExtractor> logger)
    {
        _logger = logger;
    }

    [GeneratedRegex(@"^\s*(?:Rp\.?\s*:?\s*)?(?:\d+[\.\)]\s*)?(?<name>[A-Za-zĂÂÎȘȚăâîșț][\w\s\-]*?)\s+(?<dosage>\d+\s*(?:mg|g|mcg|ml)\b)(?<rest>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex MedicationLineRegex();

    [GeneratedRegex(@"^\s*(?:Rp\.?\s*:?\s*)?\d+[\.\)]", RegexOptions.IgnoreCase)]
    private static partial Regex EntryStartRegex();

    [GeneratedRegex(@"Diagnostic\s*(?:prezumtiv)?\s*:?\s*(.{5,200})", RegexOptions.IgnoreCase)]
    private static partial Regex DiagnosisRegex();

    [GeneratedRegex(@"(?:Motiv(?:ul)?\s*(?:trimiterii|internarii)\s*:?\s*)(.{5,300})", RegexOptions.IgnoreCase)]
    private static partial Regex ReferralReasonRegex();

    [GeneratedRegex(@"(?:Dr\.?|Medic(?:\s+primar)?)\s+([A-ZĂÂÎȘȚ][a-zăâîșțA-ZĂÂÎȘȚ\s\-]{2,40})", RegexOptions.IgnoreCase)]
    private static partial Regex DoctorRegex();

    [GeneratedRegex(@"(?:Recomand[aă]ri?\s*:?\s*)(.{5,500})", RegexOptions.IgnoreCase)]
    private static partial Regex RecommendationRegex();

    [GeneratedRegex(@"(?:Concluzii?\s*:?\s*)(.{5,400})", RegexOptions.IgnoreCase)]
    private static partial Regex ConclusionRegex();

    /// <summary>Legacy overload — medication-only extraction.</summary>
    public IReadOnlyList<ExtractedHistoryItem> Extract(string? sanitizedText)
        => Extract(sanitizedText, "Prescription");

    /// <summary>
    /// Document-type-aware extraction. For Prescriptions: returns per-medication items.
    /// For other types: returns a single document-level history entry with diagnosis, reason, etc.
    /// </summary>
    public IReadOnlyList<ExtractedHistoryItem> Extract(string? sanitizedText, string docType)
    {
        if (string.IsNullOrWhiteSpace(sanitizedText))
        {
            _logger.LogDebug("[MedicalHistoryExtractor] Empty text — returning 0 items");
            return [];
        }

        _logger.LogDebug("[MedicalHistoryExtractor] Extracting from {Length} chars, docType={DocType}",
            sanitizedText.Length, docType);

        if (docType == "Prescription")
        {
            var medItems = ExtractMedications(sanitizedText);
            _logger.LogInformation("[MedicalHistoryExtractor] Prescription → {Count} medication items extracted", medItems.Count);
            return medItems;
        }

        // For non-prescription documents, build a document-level history entry
        return ExtractDocumentLevelItem(sanitizedText, docType);
    }

    // MARK: - Medication extraction (for Prescriptions)

    private IReadOnlyList<ExtractedHistoryItem> ExtractMedications(string sanitizedText)
    {
        var rawLines = sanitizedText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var merged = MergeContinuationLines(rawLines);
        var items = new List<ExtractedHistoryItem>();

        _logger.LogDebug("[MedicalHistoryExtractor] Merged {Count} lines for medication scan", merged.Count);

        foreach (var line in merged)
        {
            var match = MedicationLineRegex().Match(line);
            if (!match.Success)
            {
                _logger.LogTrace("[MedicalHistoryExtractor] No med match: {Line}", Truncate(line, 80));
                continue;
            }
            var name = match.Groups["name"].Value.Trim();
            var dosage = match.Groups["dosage"].Value.Trim();
            var rest = match.Groups["rest"].Value.Trim();
            var freq = ExtractFrequency(rest);
            var dur = ExtractDuration(rest);

            _logger.LogDebug("[MedicalHistoryExtractor] Found medication: {Name} {Dosage}, freq={Freq}, dur={Dur}",
                name, dosage, freq, dur);

            items.Add(new ExtractedHistoryItem(
                Title: $"Prescription update: {name}",
                MedicationName: name, Dosage: dosage,
                Frequency: freq, Duration: dur, Notes: rest,
                Summary: $"{name} {dosage}, {freq}".TrimEnd(',', ' '),
                Confidence: EstimateConfidence(name, dosage, freq)));
        }
        return items;
    }

    // MARK: - Document-level extraction (for Referral, Discharge, LabResult, etc.)

    private IReadOnlyList<ExtractedHistoryItem> ExtractDocumentLevelItem(string text, string docType)
    {
        var diagnosis = MatchFirst(DiagnosisRegex(), text);
        var reason = MatchFirst(ReferralReasonRegex(), text);
        var doctor = MatchFirst(DoctorRegex(), text);
        var recommendation = MatchFirst(RecommendationRegex(), text);
        var conclusion = MatchFirst(ConclusionRegex(), text);

        _logger.LogDebug(
            "[MedicalHistoryExtractor] DocLevel: diag={HasDiag}, reason={HasReason}, doc={HasDoc}, rec={HasRec}, conc={HasConc}",
            diagnosis != null, reason != null, doctor != null, recommendation != null, conclusion != null);

        // Build title, summary, and notes from what we found
        var title = BuildTitle(docType, diagnosis, reason);
        var summary = BuildSummary(docType, diagnosis, reason, doctor, recommendation, conclusion);
        var notes = BuildNotes(recommendation, conclusion);

        // If we extracted nothing meaningful, still create a basic entry so the document is tracked
        if (diagnosis == null && reason == null && recommendation == null && conclusion == null)
        {
            _logger.LogWarning("[MedicalHistoryExtractor] No structured fields found for {DocType} — creating minimal entry", docType);
            return [new ExtractedHistoryItem(
                Title: $"{FormatDocType(docType)} recorded",
                MedicationName: "", Dosage: "", Frequency: "", Duration: "",
                Notes: $"Document type: {docType}. No structured fields could be extracted.",
                Summary: $"{FormatDocType(docType)} — details pending manual review",
                Confidence: 0.30m)];
        }

        decimal confidence = 0.50m;
        if (diagnosis != null) confidence += 0.20m;
        if (reason != null) confidence += 0.10m;
        if (doctor != null) confidence += 0.05m;
        if (recommendation != null) confidence += 0.10m;
        if (conclusion != null) confidence += 0.05m;

        _logger.LogInformation("[MedicalHistoryExtractor] {DocType} → 1 document-level item, confidence={Conf:F2}", docType, confidence);

        return [new ExtractedHistoryItem(
            Title: title,
            MedicationName: "", Dosage: "", Frequency: "", Duration: "",
            Notes: notes,
            Summary: summary,
            Confidence: Math.Min(1.0m, confidence))];
    }

    private static string BuildTitle(string docType, string? diagnosis, string? reason)
    {
        var label = FormatDocType(docType);
        if (diagnosis != null) return $"{label}: {Truncate(diagnosis, 80)}";
        if (reason != null) return $"{label}: {Truncate(reason, 80)}";
        return $"{label} recorded";
    }

    private static string BuildSummary(string docType, string? diagnosis, string? reason,
        string? doctor, string? recommendation, string? conclusion)
    {
        var parts = new List<string>();
        parts.Add(FormatDocType(docType));
        if (diagnosis != null) parts.Add($"Diagnosis: {Truncate(diagnosis, 100)}");
        if (reason != null) parts.Add($"Reason: {Truncate(reason, 100)}");
        if (doctor != null) parts.Add($"Doctor: {doctor}");
        if (recommendation != null) parts.Add($"Rec: {Truncate(recommendation, 120)}");
        if (conclusion != null) parts.Add($"Conclusion: {Truncate(conclusion, 120)}");
        return string.Join(". ", parts);
    }

    private static string BuildNotes(string? recommendation, string? conclusion)
    {
        var parts = new List<string>();
        if (recommendation != null) parts.Add($"Recommendations: {recommendation}");
        if (conclusion != null) parts.Add($"Conclusions: {conclusion}");
        return parts.Count > 0 ? string.Join("\n", parts) : "";
    }

    private static string FormatDocType(string docType) => docType switch
    {
        "Referral" => "Medical referral",
        "Discharge" => "Discharge summary",
        "LabResult" => "Lab results",
        "MedicalCertificate" => "Medical certificate",
        "ImagingReport" => "Imaging report",
        "EcgReport" => "ECG report",
        "OperativeReport" => "Operative report",
        "ConsultationNote" => "Consultation note",
        _ => $"Medical document ({docType})"
    };

    private static string? MatchFirst(Regex regex, string text)
    {
        var m = regex.Match(text);
        if (!m.Success) return null;
        var val = m.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(val) ? null : val;
    }

    private static string Truncate(string? text, int maxLength)
    {
        if (text == null) return "";
        return text.Length <= maxLength ? text : text[..maxLength] + "…";
    }

    // MARK: - Shared helpers

    private static List<string> MergeContinuationLines(string[] lines)
    {
        var merged = new List<string>();
        foreach (var line in lines)
        {
            if (EntryStartRegex().IsMatch(line) || merged.Count == 0)
                merged.Add(line);
            else if (MedicationLineRegex().IsMatch(line))
                merged.Add(line);
            else
                merged[^1] = $"{merged[^1]} {line}";
        }
        return merged;
    }

    private static string ExtractFrequency(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("diminea")) return "Morning";
        if (lower.Contains("seara")) return "Evening";
        if (lower.Contains("zi") || lower.Contains("/zi")) return "Daily";
        return "As prescribed";
    }

    private static string ExtractDuration(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("lung")) return "Long term";
        if (lower.Contains("continuu")) return "Continuous";
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
