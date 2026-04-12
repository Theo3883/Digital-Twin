using System.Text.RegularExpressions;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.OCR.Services;

/// <summary>
/// Heuristic field extraction from OCR text.
/// </summary>
public sealed partial class HeuristicFieldExtractor : IHeuristicFieldExtractor
{
    [GeneratedRegex(@"\b(\d{13})\b")]
    private static partial Regex CnpRegex();
    [GeneratedRegex(@"\b(\d{1,2}[./]\d{1,2}[./]\d{4})\b")]
    private static partial Regex DateRegex();
    [GeneratedRegex(@"(?:Nume|Pacient|Numar)\s*:?\s*([A-ZĂÂÎȘȚ][a-zăâîșțA-ZĂÂÎȘȚ\s\-]{2,40})")]
    private static partial Regex NameAfterLabelRegex();
    [GeneratedRegex(@"(?:Dr\.?|Medic(?:\s+primar)?)\s+([A-ZĂÂÎȘȚ][a-zăâîșțA-ZĂÂÎȘȚ\s\-]{2,40})")]
    private static partial Regex DoctorRegex();
    [GeneratedRegex(@"Diagnostic\s*(?:prezumtiv)?\s*:?\s*(.{5,120})", RegexOptions.IgnoreCase)]
    private static partial Regex DiagnosisRegex();
    [GeneratedRegex(@"(?:Rp\.?\s*:?\s*)?\d+[\.\)]\s*([A-Za-zĂÂÎȘȚăâîșț][\w\s\-]*?)\s+(\d+\s*(?:mg|g|mcg|ml)\b)(.*)", RegexOptions.IgnoreCase)]
    private static partial Regex MedicationRegex();

    public HeuristicExtractionResult Extract(string? rawText, string docType)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return HeuristicExtractionResult.Empty;

        var pid = ExtractField(CnpRegex(), rawText);
        var pname = ExtractField(NameAfterLabelRegex(), rawText);
        var date = ExtractField(DateRegex(), rawText);
        var doc = ExtractField(DoctorRegex(), rawText);
        var diag = (docType is "Referral" or "Discharge" or "ConsultationNote")
            ? ExtractField(DiagnosisRegex(), rawText) : null;
        var meds = docType == "Prescription" ? ExtractMedications(rawText) : [];

        return new HeuristicExtractionResult(pname, pid, date, doc, diag, meds);
    }

    private static string? ExtractField(Regex regex, string text)
    {
        var m = regex.Match(text);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static List<ExtractedMedicationField> ExtractMedications(string text)
    {
        var results = new List<ExtractedMedicationField>();
        foreach (Match m in MedicationRegex().Matches(text))
            results.Add(new ExtractedMedicationField(
                m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim(),
                null, m.Groups[3].Value.Trim()));
        return results;
    }
}
