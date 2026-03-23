using System.Text.RegularExpressions;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Redacts PII/PHI from OCR text for safe preview display and logging.
/// This is NOT document editing — the source document is never altered.
/// Raw OCR text must never appear in logs; always pass through this sanitizer first.
/// </summary>
public sealed class SensitiveDataSanitizer
{
    private static readonly (Regex Pattern, string Replacement)[] Rules =
    [
        // Romanian CNP (Personal Numeric Code) — 13 digits starting with 1-8
        (new Regex(@"\b[1-8]\d{12}\b", RegexOptions.Compiled), "[CNP]"),

        // Email addresses
        (new Regex(@"\b[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}\b", RegexOptions.Compiled), "[EMAIL]"),

        // Romanian phone numbers (+40 or 07x format)
        (new Regex(@"(\+40|0040)?[\s.\-]?(7[0-9]{2}|2[1-9][0-9]|3[0-9]{2})[\s.\-]?\d{3}[\s.\-]?\d{3}\b", RegexOptions.Compiled), "[PHONE]"),

        // Bearer / JWT tokens
        (new Regex(@"Bearer\s+[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+", RegexOptions.Compiled | RegexOptions.IgnoreCase), "[TOKEN]"),

        // Simple medical IDs (e.g. Romanian CNAS code patterns: prefix + 10+ digits)
        (new Regex(@"\b(PNS|CNAS|MED)\d{6,}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "[MED-ID]"),

        // Long numeric sequences (12+ digits not already matched as CNP)
        (new Regex(@"\b\d{12,}\b", RegexOptions.Compiled), "[NUM]"),

        // Calendar dates with year (dd.mm.yyyy, dd/mm/yyyy, yyyy-mm-dd)
        (new Regex(@"\b(\d{1,2}[./]\d{1,2}[./]\d{4}|\d{4}-\d{2}-\d{2})\b", RegexOptions.Compiled), "[DATE]"),
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
}
