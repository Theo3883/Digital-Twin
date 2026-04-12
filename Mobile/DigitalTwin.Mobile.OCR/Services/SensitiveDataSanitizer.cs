using System.Text.RegularExpressions;
using DigitalTwin.Mobile.Domain.Interfaces;

namespace DigitalTwin.Mobile.OCR.Services;

/// <summary>
/// Redacts PII/PHI from OCR text for safe preview display.
/// </summary>
public sealed partial class SensitiveDataSanitizer : ISensitiveDataSanitizer
{
    [GeneratedRegex(@"\b[1-8]\d{12}\b")]
    private static partial Regex CnpPattern();

    [GeneratedRegex(@"\b[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}\b")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(\+40|0040)?[\s.\-]?(7[0-9]{2}|2[1-9][0-9]|3[0-9]{2})[\s.\-]?\d{3}[\s.\-]?\d{3}\b")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"\b(PNS|CNAS|MED)\d{6,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex MedIdPattern();

    [GeneratedRegex(@"\b\d{12,}\b")]
    private static partial Regex LongNumericPattern();

    [GeneratedRegex(@"\b(\d{1,2}[./]\d{1,2}[./]\d{4}|\d{4}-\d{2}-\d{2})\b")]
    private static partial Regex DatePattern();

    private static readonly (Regex Pattern, string Replacement)[] Rules =
    [
        (CnpPattern(), "[CNP]"),
        (EmailPattern(), "[EMAIL]"),
        (PhonePattern(), "[PHONE]"),
        (MedIdPattern(), "[MED-ID]"),
        (LongNumericPattern(), "[NUM]"),
        (DatePattern(), "[DATE]"),
    ];

    public string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var result = text;
        foreach (var (pattern, replacement) in Rules)
            result = pattern.Replace(result, replacement);
        return result;
    }

    public string BuildSanitizedPreview(IEnumerable<string> pageTexts, int maxLength = 2000)
    {
        var combined = string.Join("\n---\n", pageTexts);
        var sanitized = Sanitize(combined);
        return sanitized.Length <= maxLength ? sanitized : sanitized[..maxLength] + "\n[…truncated]";
    }
}
