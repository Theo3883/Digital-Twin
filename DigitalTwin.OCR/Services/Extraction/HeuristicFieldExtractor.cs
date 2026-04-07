using System.Text.RegularExpressions;
using DigitalTwin.Domain.Enums;
using DigitalTwin.OCR.Models.Structured;

namespace DigitalTwin.OCR.Services.Extraction;

/// <summary>
/// Extracts structured fields from raw OCR text using regex/keyword heuristics.
/// This is the Phase 1 extractor and the permanent fallback when ML extraction is disabled.
/// </summary>
public sealed partial class HeuristicFieldExtractor
{
    // CNP: 13-digit Romanian personal numeric code
    [GeneratedRegex(@"\b(\d{13})\b")]
    private static partial Regex CnpRegex();
    // Date: DD.MM.YYYY or DD/MM/YYYY
    [GeneratedRegex(@"\b(\d{1,2}[./]\d{1,2}[./]\d{4})\b")]
    private static partial Regex DateRegex();
    // Name after "Nume:" / "Pacient:" labels
    [GeneratedRegex(@"(?:Nume|Pacient|Numar)\s*:?\s*([A-ZĂÂÎȘȚ][a-zăâîșțA-ZĂÂÎȘȚ\s\-]{2,40})")]
    private static partial Regex NameAfterLabelRegex();
    // Doctor name after "Dr." / "Medic" labels
    [GeneratedRegex(@"(?:Dr\.?|Medic(?:\s+primar)?)\s+([A-ZĂÂÎȘȚ][a-zăâîșțA-ZĂÂÎȘȚ\s\-]{2,40})")]
    private static partial Regex DoctorRegex();
    // Diagnosis after "Diagnostic:" label
    [GeneratedRegex(@"Diagnostic\s*(?:prezumtiv)?\s*:?\s*(.{5,120})", RegexOptions.IgnoreCase)]
    private static partial Regex DiagnosisRegex();
    // Medication: numbered lines with dosage
    [GeneratedRegex(@"(?:Rp\.?\s*:?\s*)?\d+[\.\)]\s*([A-Za-zĂÂÎȘȚăâîșț][\w\s\-]*?)\s+(\d+\s*(?:mg|g|mcg|ml)\b)(.*)", RegexOptions.IgnoreCase)]
    private static partial Regex MedicationRegex();

    public HeuristicExtractionResult Extract(string? rawText, MedicalDocumentType docType)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return HeuristicExtractionResult.Empty;

        var patientId = ExtractCnp(rawText);
        var patientName = ExtractPatientName(rawText);
        var reportDate = ExtractDate(rawText);
        var doctorName = ExtractDoctor(rawText);
        var diagnosis = docType is MedicalDocumentType.Referral or MedicalDocumentType.Discharge or MedicalDocumentType.ConsultationNote
            ? ExtractDiagnosis(rawText) : null;
        var medications = docType is MedicalDocumentType.Prescription
            ? ExtractMedications(rawText) : [];

        return new HeuristicExtractionResult(
            PatientName: patientName,
            PatientId: patientId,
            ReportDate: reportDate,
            DoctorName: doctorName,
            Diagnosis: diagnosis,
            Medications: medications);
    }

    private static ExtractedField<string>? ExtractCnp(string text)
    {
        var m = CnpRegex().Match(text);
        return m.Success
            ? new ExtractedField<string>(m.Groups[1].Value, 0.92f, ExtractionMethod.HeuristicRegex)
            : null;
    }

    private static ExtractedField<string>? ExtractPatientName(string text)
    {
        var m = NameAfterLabelRegex().Match(text);
        if (!m.Success) return null;
        var name = m.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(name)
            ? null
            : new ExtractedField<string>(name, 0.75f, ExtractionMethod.HeuristicRegex);
    }

    private static ExtractedField<string>? ExtractDate(string text)
    {
        var m = DateRegex().Match(text);
        return m.Success
            ? new ExtractedField<string>(m.Groups[1].Value, 0.85f, ExtractionMethod.HeuristicRegex)
            : null;
    }

    private static ExtractedField<string>? ExtractDoctor(string text)
    {
        var m = DoctorRegex().Match(text);
        if (!m.Success) return null;
        var name = m.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(name)
            ? null
            : new ExtractedField<string>(name, 0.72f, ExtractionMethod.HeuristicRegex);
    }

    private static ExtractedField<string>? ExtractDiagnosis(string text)
    {
        var m = DiagnosisRegex().Match(text);
        if (!m.Success) return null;
        var value = m.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? null
            : new ExtractedField<string>(value, 0.70f, ExtractionMethod.HeuristicRegex);
    }

    private static IReadOnlyList<ExtractedMedication> ExtractMedications(string text)
    {
        var results = new List<ExtractedMedication>();
        foreach (Match m in MedicationRegex().Matches(text))
        {
            var name = m.Groups[1].Value.Trim();
            var dose = m.Groups[2].Value.Trim();
            var rest = m.Groups[3].Value.Trim();

            results.Add(new ExtractedMedication(
                Name: new ExtractedField<string>(name, 0.80f, ExtractionMethod.HeuristicRegex),
                Dose: string.IsNullOrWhiteSpace(dose) ? null
                    : new ExtractedField<string>(dose, 0.78f, ExtractionMethod.HeuristicRegex),
                Frequency: ExtractFrequencyField(rest),
                Route: null,
                Duration: ExtractDurationField(rest)));
        }
        return results;
    }

    private static ExtractedField<string>? ExtractFrequencyField(string rest)
    {
        var lower = rest.ToLowerInvariant();
        string? freq = lower.Contains("dimineata") || lower.Contains("diminea") ? "Morning"
            : lower.Contains("seara") ? "Evening"
            : lower.Contains("/zi") || lower.Contains("cp/zi") ? "Daily"
            : null;
        return freq is null ? null : new ExtractedField<string>(freq, 0.68f, ExtractionMethod.HeuristicRegex);
    }

    private static ExtractedField<string>? ExtractDurationField(string rest)
    {
        var lower = rest.ToLowerInvariant();
        string? dur = lower.Contains("lung") ? "Long term"
            : lower.Contains("continuu") ? "Continuous"
            : null;
        return dur is null ? null : new ExtractedField<string>(dur, 0.65f, ExtractionMethod.HeuristicRegex);
    }
}

public sealed record HeuristicExtractionResult(
    ExtractedField<string>? PatientName,
    ExtractedField<string>? PatientId,
    ExtractedField<string>? ReportDate,
    ExtractedField<string>? DoctorName,
    ExtractedField<string>? Diagnosis,
    IReadOnlyList<ExtractedMedication> Medications)
{
    public static HeuristicExtractionResult Empty =>
        new(null, null, null, null, null, []);
}
