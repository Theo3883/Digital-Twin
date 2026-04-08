using System.Text.RegularExpressions;
using DigitalTwin.OCR.Models.Structured;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Redacts PII/PHI from OCR text for safe preview display and logging.
/// This is NOT document editing — the source document is never altered.
/// Raw OCR text must never appear in logs; always pass through this sanitizer first.
/// </summary>
public sealed partial class SensitiveDataSanitizer
{
    [GeneratedRegex(@"\b[1-8]\d{12}\b")]
    private static partial Regex CnpPattern();

    [GeneratedRegex(@"\b[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}\b")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(\+40|0040)?[\s.\-]?(7[0-9]{2}|2[1-9][0-9]|3[0-9]{2})[\s.\-]?\d{3}[\s.\-]?\d{3}\b")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+", RegexOptions.IgnoreCase)]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"\b(PNS|CNAS|MED)\d{6,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex MedIdPattern();

    [GeneratedRegex(@"\b\d{12,}\b")]
    private static partial Regex LongNumericPattern();

    [GeneratedRegex(@"\b(\d{1,2}[./]\d{1,2}[./]\d{4}|\d{4}-\d{2}-\d{2})\b")]
    private static partial Regex DatePattern();

    private static readonly (Regex Pattern, string Replacement)[] Rules =
    [
        // Romanian CNP (Personal Numeric Code) — 13 digits starting with 1-8
        (CnpPattern(), "[CNP]"),

        // Email addresses
        (EmailPattern(), "[EMAIL]"),

        // Romanian phone numbers (+40 or 07x format)
        (PhonePattern(), "[PHONE]"),

        // Bearer / JWT tokens
        (TokenPattern(), "[TOKEN]"),

        // Simple medical IDs (e.g. Romanian CNAS code patterns: prefix + 10+ digits)
        (MedIdPattern(), "[MED-ID]"),

        // Long numeric sequences (12+ digits not already matched as CNP)
        (LongNumericPattern(), "[NUM]"),

        // Calendar dates with year (dd.mm.yyyy, dd/mm/yyyy, yyyy-mm-dd)
        (DatePattern(), "[DATE]"),
    ];

    /// <summary>
    /// Returns a sanitized copy of <paramref name="text"/> safe for logs and preview display.
    /// The original text is never mutated.
    /// </summary>
    public string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var result = text;
        foreach (var (pattern, replacement) in Rules)
            result = pattern.Replace(result, replacement);

        return result;
    }

    /// <summary>Sanitizes each page and returns the combined preview (max 2000 chars).</summary>
    public string BuildSanitizedPreview(IEnumerable<string> pageTexts, int maxLength = 2000)
    {
        var combined = string.Join("\n---\n", pageTexts);
        var sanitized = Sanitize(combined);

        if (sanitized.Length <= maxLength)
            return sanitized;

        return sanitized[..maxLength] + "\n[…truncated for preview]";
    }

    /// <summary>
    /// Redacts all PII fields from a StructuredMedicalDocument for safe logging/display.
    /// Returns a new instance with sensitive fields replaced by placeholder tokens.
    /// The original document is never mutated.
    /// </summary>
    public StructuredMedicalDocument RedactStructured(StructuredMedicalDocument doc)
    {
        static ExtractedField<string>? RedactField(ExtractedField<string>? field, string placeholder) =>
            field is null ? null : field with { Value = placeholder };

        return doc with
        {
            PatientName = RedactField(doc.PatientName, "[PATIENT_NAME]"),
            PatientId   = RedactField(doc.PatientId,   "[CNP]"),
            DateOfBirth = RedactField(doc.DateOfBirth, "[DOB]"),
            // DoctorName is not PII of the patient — keep it
            // Diagnosis may be PHI in some contexts — keep but note it
            ReportDate  = RedactField(doc.ReportDate,  "[DATE]"),
        };
    }
}
