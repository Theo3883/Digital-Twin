using System.Text.RegularExpressions;
using DigitalTwin.OCR.Models;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Extracts patient identity (name and CNP) from raw OCR text.
/// Handles both labeled formats ("Nume: Popescu Ion") and unlabeled formats
/// where the name appears as a standalone line near known field anchors.
/// </summary>
public sealed partial class DocumentIdentityExtractorService
{
    [GeneratedRegex(@"\b[1-8]\d{12}\b")]
    private static partial Regex CnpRegex();

    [GeneratedRegex(@"(?:Nume\s+pacient(?:ului)?|Nume(?:\s+(?:și|si)\s+prenume(?:le)?)?|Prenume|Pacient)\s*[:\-–]?\s*(?<name>.+)", RegexOptions.IgnoreCase)]
    private static partial Regex LabeledNameRegex();

    [GeneratedRegex(@"\b\d{1,2}[./]\d{1,2}[./]\d{4}\b|\b\d{4}-\d{2}-\d{2}\b")]
    private static partial Regex DateRegex();

    // Strips inline fields that appear on the same OCR line as the name
    // (e.g. discharge letters format "Sandu Teodor    Vârstă: 68 ani" on one line).
    [GeneratedRegex(@"\s+(?:V[aâ]rst[aă]|V[aâ]rsta|CNP|Adres[aă]|Data|Sex|Prenume|Medic|Diagnostic)\s*[:\-\u2013].*$", RegexOptions.IgnoreCase)]
    private static partial Regex InlineFieldRegex();

    [GeneratedRegex(@"(\+40|0040)?[\s.\-]?(7[0-9]{2}|2[1-9][0-9]|3[0-9]{2})[\s.\-]?\d{3}[\s.\-]?\d{3}")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"^\d[\d\s./\-]*$")]
    private static partial Regex PurelyNumericRegex();

    /// <summary>
    /// Known Romanian medical document field labels that indicate a line is NOT a patient name.
    /// </summary>
    private static readonly string[] FieldAnchors =
    [
        "data", "sex", "cnp", "telefon", "tel", "medic", "diagnostic",
        "laborator", "nr", "cod", "adresa", "vârsta", "varsta", "grupa",
        "rezultat", "analiz", "probe", "recoltare", "validat", "data nasterii",
        "data nașterii", "sectie", "secția", "spital", "clinica"
    ];

    /// <summary>
    /// Alphabetic characters valid in Romanian names (letters, hyphens, spaces, diacritics).
    /// </summary>
    [GeneratedRegex(@"^[A-Za-zĂÂÎȘȚăâîșțÁÉÍÓÚáéíóú\-\s]+$")]
    private static partial Regex NameCandidateRegex();

    /// <summary>
    /// Extracts name and CNP from the given raw OCR text.
    /// </summary>
    public static DocumentIdentity Extract(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return new DocumentIdentity(null, null, 0f, 0f);

        var cnp = ExtractCnp(rawText);
        var (name, nameConfidence) = ExtractName(rawText);

        var cnpConfidence = cnp is not null ? 1.0f : 0f;

        return new DocumentIdentity(name, cnp, nameConfidence, cnpConfidence);
    }

    private static string? ExtractCnp(string text)
    {
        var match = CnpRegex().Match(text);
        return match.Success ? match.Value : null;
    }

    private static (string? Name, float Confidence) ExtractName(string text)
    {
        // Strategy A: Try ALL labeled name matches (highest confidence).
        // Some documents (e.g. discharge letters) have an empty "Nume: _____" line before
        // the real one — iterating all matches ensures we find the first plausible name.
        foreach (Match labelMatch in LabeledNameRegex().Matches(text))
        {
            var name = CleanNameValue(labelMatch.Groups["name"].Value);
            if (IsPlausibleName(name))
                return (name, 0.95f);
        }

        // Strategy B: Zone-based unlabeled extraction
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ExtractUnlabeledName(lines);
    }

    private static (string? Name, float Confidence) ExtractUnlabeledName(string[] lines)
    {
        // Walk lines: find name candidates that appear near known field anchors
        // (i.e., in the patient header block of a medical document).
        string? bestCandidate = null;
        float bestConfidence = 0f;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Skip empty, known labels, dates, phone numbers, CNP, purely numeric
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (IsFieldAnchor(line)) continue;
            if (CnpRegex().IsMatch(line)) continue;
            if (DateRegex().IsMatch(line)) continue;
            if (PhoneRegex().IsMatch(line)) continue;
            if (PurelyNumericRegex().IsMatch(line)) continue;

            // Must look like a name: 2-4 alphabetic words
            if (!NameCandidateRegex().IsMatch(line)) continue;

            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (words.Length < 2 || words.Length > 5) continue;

            // Must have at least one word starting with uppercase
            if (!words.Any(w => char.IsUpper(w[0]))) continue;

            // Score based on proximity to known anchors
            var confidence = ScoreNameCandidate(lines, i);

            if (confidence > bestConfidence)
            {
                bestCandidate = string.Join(" ", words);
                bestConfidence = confidence;
            }
        }

        return (bestCandidate, bestConfidence);
    }

    private static float ScoreNameCandidate(string[] lines, int candidateIndex)
    {
        var score = 0.5f; // Base score for being alphabetic 2-4 word line

        // Bonus: appears in the first 10 lines (patient header is typically at the top)
        if (candidateIndex < 10) score += 0.1f;

        // Bonus: a known field anchor exists within ±3 lines
        var nearbyAnchors = 0;
        for (var j = Math.Max(0, candidateIndex - 3); j < Math.Min(lines.Length, candidateIndex + 4); j++)
        {
            if (j == candidateIndex) continue;
            if (IsFieldAnchor(lines[j]))
                nearbyAnchors++;
        }

        if (nearbyAnchors >= 1) score += 0.15f;
        if (nearbyAnchors >= 2) score += 0.10f;

        // Bonus: CNP is found within ±5 lines
        for (var j = Math.Max(0, candidateIndex - 5); j < Math.Min(lines.Length, candidateIndex + 6); j++)
        {
            if (j == candidateIndex) continue;
            if (CnpRegex().IsMatch(lines[j]))
            {
                score += 0.15f;
                break;
            }
        }

        return Math.Min(1.0f, score);
    }

    private static bool IsFieldAnchor(string line)
    {
        var lower = line.ToLowerInvariant().Trim();
        return FieldAnchors.Any(a => lower.StartsWith(a, StringComparison.Ordinal));
    }

    private static string CleanNameValue(string raw)
    {
        // Strip inline fields that appear on the same OCR line as the name
        // (e.g. "Sandu Teodor    Vârstă: 68 ani" → "Sandu Teodor")
        var cleaned = InlineFieldRegex().Replace(raw, "").Trim();
        // Remove blank-field underscores (e.g. "_____")
        cleaned = Regex.Replace(cleaned, @"_+", "").Trim();
        // Remove trailing dates, numbers, special chars
        cleaned = DateRegex().Replace(cleaned, "").Trim();
        cleaned = PurelyNumericRegex().Replace(cleaned, "").Trim();
        // Remove trailing punctuation
        return cleaned.TrimEnd(':', '-', '–', ' ');
    }

    private static bool IsPlausibleName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length >= 2 && words.Length <= 5;
    }
}
