using System.Text.RegularExpressions;
using DigitalTwin.Mobile.Domain.Enums;

namespace DigitalTwin.Mobile.Domain.Services;

/// <summary>
/// Redacts PII/PHI from OCR text for safe preview display.
/// </summary>
public sealed partial class SensitiveDataSanitizer
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

/// <summary>
/// Keyword-based document type classifier.
/// </summary>
public sealed class DocumentTypeClassifier
{
    public static string Classify(string? ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
            return "Unknown";

        var text = ocrText.ToUpperInvariant();

        if (text.Contains("RP.:") || text.Contains("RP:") ||
            text.Contains("REȚETĂ") || text.Contains("RETETA"))
            return "Prescription";

        if (text.Contains("BILET DE TRIMITERE") || text.Contains("MOTIVUL TRIMITERII"))
            return "Referral";

        if (text.Contains("BULETIN DE ANALIZE") ||
            (text.Contains("REZULTAT") && (text.Contains("VALORI DE REFERINȚĂ") || text.Contains("VALORI DE REFERINTA"))))
            return "LabResult";

        if (text.Contains("SCRISOARE MEDICALĂ") || text.Contains("SCRISOARE MEDICALA") ||
            text.Contains("BILET DE IEȘIRE") || text.Contains("BILET DE IESIRE") ||
            text.Contains("EPICRIZĂ") || text.Contains("EPICRIZA"))
            return "Discharge";

        if (text.Contains("CERTIFICAT MEDICAL") || text.Contains("ADEVERINȚĂ MEDICALĂ") ||
            text.Contains("CONCEDIU MEDICAL"))
            return "MedicalCertificate";

        if (text.Contains("ECOGRAFIE") || text.Contains("RADIOGRAFIE") ||
            text.Contains("TOMOGRAFIE") || text.Contains("DESCRIERE IMAGISTICĂ"))
            return "ImagingReport";

        if (text.Contains("ELECTROCARDIOGRAMĂ") || text.Contains("ELECTROCARDIOGRAMA") ||
            (text.Contains("ECG") && (text.Contains("RITM") || text.Contains("FRECVENTA CARDIACA"))))
            return "EcgReport";

        if (text.Contains("PROTOCOL OPERATOR") || text.Contains("INTERVENTIE CHIRURGICALA") ||
            text.Contains("INTERVENȚIE CHIRURGICALĂ"))
            return "OperativeReport";

        if (text.Contains("CONSULTAȚIE DE SPECIALITATE") || text.Contains("CONSULTATIE DE SPECIALITATE") ||
            text.Contains("EXAMEN CLINIC") || text.Contains("EXAMEN OBIECTIV"))
            return "ConsultationNote";

        return "Unknown";
    }
}

/// <summary>
/// Name matching with Romanian diacritics normalization and Levenshtein distance.
/// </summary>
public sealed class NameMatchingService
{
    private const int MaxDistancePerToken = 2;

    public Models.NameMatchResult Match(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
            return new Models.NameMatchResult(false, int.MaxValue, Normalize(expected ?? ""), Normalize(actual ?? ""));

        var ne = Normalize(expected);
        var na = Normalize(actual);

        if (ne == na) return new Models.NameMatchResult(true, 0, ne, na);

        var et = Tokenize(ne);
        var at = Tokenize(na);

        var sorted = CompareTokensSorted(et, at);
        if (sorted.IsMatch) return sorted with { NormalizedExpected = ne, NormalizedActual = na };

        var subset = CompareTokensSubset(et, at);
        return subset with { NormalizedExpected = ne, NormalizedActual = na };
    }

    private static Models.NameMatchResult CompareTokensSorted(string[] expected, string[] actual)
    {
        var se = expected.OrderBy(t => t, StringComparer.Ordinal).ToArray();
        var sa = actual.OrderBy(t => t, StringComparer.Ordinal).ToArray();
        if (se.Length != sa.Length) return new Models.NameMatchResult(false, int.MaxValue, "", "");

        var total = 0;
        for (var i = 0; i < se.Length; i++)
        {
            var d = LevenshteinDistance(se[i], sa[i]);
            if (d > MaxDistancePerToken) return new Models.NameMatchResult(false, int.MaxValue, "", "");
            total += d;
        }
        return new Models.NameMatchResult(true, total, "", "");
    }

    private static Models.NameMatchResult CompareTokensSubset(string[] expected, string[] actual)
    {
        if (actual.Length < expected.Length) return new Models.NameMatchResult(false, int.MaxValue, "", "");
        var used = new HashSet<int>();
        var total = 0;
        foreach (var et in expected)
        {
            var best = int.MaxValue; var bi = -1;
            for (var j = 0; j < actual.Length; j++)
            {
                if (used.Contains(j)) continue;
                var d = LevenshteinDistance(et, actual[j]);
                if (d < best) { best = d; bi = j; }
            }
            if (bi < 0 || best > MaxDistancePerToken) return new Models.NameMatchResult(false, int.MaxValue, "", "");
            used.Add(bi); total += best;
        }
        return new Models.NameMatchResult(true, total, "", "");
    }

    internal static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var lower = name.ToLowerInvariant();
        return StripDiacritics(lower).Trim();
    }

    private static string[] Tokenize(string n) =>
        n.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string StripDiacritics(string text)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = chars[i] switch
            {
                'ă' or 'â' => 'a', 'î' => 'i', 'ș' => 's', 'ț' => 't',
                'á' or 'à' or 'ä' => 'a', 'é' or 'è' or 'ë' => 'e',
                'í' or 'ì' or 'ï' => 'i', 'ó' or 'ò' or 'ö' => 'o',
                'ú' or 'ù' or 'ü' => 'u',
                _ => chars[i]
            };
        }
        return new string(chars);
    }

    internal static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;
        var n = s.Length; var m = t.Length;
        var prev = new int[m + 1]; var curr = new int[m + 1];
        for (var j = 0; j <= m; j++) prev[j] = j;
        for (var i = 1; i <= n; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= m; j++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[m];
    }
}

/// <summary>
/// Extracts patient identity (name and CNP) from raw OCR text.
/// </summary>
public sealed partial class DocumentIdentityExtractor
{
    [GeneratedRegex(@"\b[1-8]\d{12}\b")]
    private static partial Regex CnpRegex();

    [GeneratedRegex(@"(?:CNP)\s*[:\-–]?\s*([1-8][\d\s.]{11,15}\d)", RegexOptions.IgnoreCase)]
    private static partial Regex LabeledCnpRegex();

    [GeneratedRegex(@"\b[1-8][\d\s]{12,16}\b")]
    private static partial Regex SpaceTolerantCnpRegex();

    [GeneratedRegex(
        @"(?m)^\s*(?:Nume\s+pacient(?:ului)?|Nume(?:\s+(?:și|si)\s+prenume(?:le)?)?|Prenume|Pacient)\b\s*[:\-–]?\s*(?<name>.+)\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex LabeledNameRegex();

    [GeneratedRegex(@"\b\d{1,2}[./]\d{1,2}[./]\d{4}\b|\b\d{4}-\d{2}-\d{2}\b")]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"\s+(?:V[aâ]rst[aă]|CNP|Adres[aă]|Data|Sex|Prenume|Medic|Diagnostic)\s*[:\-\u2013].*$", RegexOptions.IgnoreCase)]
    private static partial Regex InlineFieldRegex();

    [GeneratedRegex(@"(\+40|0040)?[\s.\-]?(7[0-9]{2}|2[1-9][0-9]|3[0-9]{2})[\s.\-]?\d{3}[\s.\-]?\d{3}")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"^\d[\d\s./\-]*$")]
    private static partial Regex PurelyNumericRegex();

    [GeneratedRegex(@"\b(arcadia|policlinic(?:a)?|spital(?:ul)?|laborator(?:ul)?|radiologie|centru(?:l)?|clinica|medicale)\b", RegexOptions.IgnoreCase)]
    private static partial Regex InstitutionLineRegex();

    [GeneratedRegex(@"^[A-Za-zĂÂÎȘȚăâîșțÁÉÍÓÚáéíóú\-\s]+$")]
    private static partial Regex NameCandidateRegex();

    private static readonly string[] FieldAnchors =
    [
        "data", "sex", "cnp", "telefon", "tel", "medic", "diagnostic",
        "laborator", "nr", "cod", "adresa", "vârsta", "varsta", "grupa",
        "rezultat", "analiz", "probe", "recoltare", "validat", "data nasterii",
        "data nașterii", "sectie", "secția", "spital", "clinica", "locatie", "locație"
    ];

    private static readonly Dictionary<char, char> DigitConfusions = new()
    {
        ['O'] = '0', ['o'] = '0', ['I'] = '1', ['l'] = '1', ['|'] = '1',
        ['S'] = '5', ['s'] = '5', ['B'] = '8',
    };

    public Models.DocumentIdentity Extract(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return new Models.DocumentIdentity(null, null, 0f, 0f);

        var (cnp, _) = ExtractCnpWithFallbacks(rawText);
        var (name, nameConfidence) = ExtractName(rawText);
        var cnpConfidence = cnp is not null ? 1.0f : 0f;

        return new Models.DocumentIdentity(name, cnp, nameConfidence, cnpConfidence);
    }

    private static (string? Cnp, string Strategy) ExtractCnpWithFallbacks(string text)
    {
        var match = CnpRegex().Match(text);
        if (match.Success) return (match.Value, "exact");

        var normalized = NormalizeDigits(text);
        match = CnpRegex().Match(normalized);
        if (match.Success) return (match.Value, "exact-normalized");

        var labelMatch = LabeledCnpRegex().Match(text);
        if (labelMatch.Success)
        {
            var candidate = Regex.Replace(labelMatch.Groups[1].Value, @"[\s.]", "");
            if (candidate.Length == 13 && candidate.All(char.IsDigit))
                return (candidate, "label-anchored");
        }

        foreach (Match stMatch in SpaceTolerantCnpRegex().Matches(text))
        {
            var candidate = Regex.Replace(stMatch.Value, @"\s", "");
            if (candidate.Length == 13 && candidate.All(char.IsDigit) && candidate[0] >= '1' && candidate[0] <= '8')
                return (candidate, "space-tolerant");
        }

        foreach (Match stMatch in SpaceTolerantCnpRegex().Matches(normalized))
        {
            var candidate = Regex.Replace(stMatch.Value, @"\s", "");
            if (candidate.Length == 13 && candidate.All(char.IsDigit) && candidate[0] >= '1' && candidate[0] <= '8')
                return (candidate, "space-tolerant-normalized");
        }

        return (null, "none");
    }

    private static string NormalizeDigits(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var chars = input.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (DigitConfusions.TryGetValue(chars[i], out var mapped))
                chars[i] = mapped;
        return new string(chars);
    }

    private static (string? Name, float Confidence) ExtractName(string text)
    {
        foreach (Match labelMatch in LabeledNameRegex().Matches(text))
        {
            var name = CleanNameValue(labelMatch.Groups["name"].Value);
            if (IsPlausibleName(name)) return (name, 0.95f);
        }

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ExtractUnlabeledName(lines);
    }

    private static (string?, float) ExtractUnlabeledName(string[] lines)
    {
        string? best = null; float bestConf = 0f;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = StripNonNameTail(lines[i].Trim());
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (IsFieldAnchor(line) || InstitutionLineRegex().IsMatch(line)) continue;
            if (CnpRegex().IsMatch(line) || DateRegex().IsMatch(line)) continue;
            if (PhoneRegex().IsMatch(line) || PurelyNumericRegex().IsMatch(line)) continue;
            if (!NameCandidateRegex().IsMatch(line)) continue;
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2 || words.Length > 5) continue;
            if (!words.Any(w => char.IsUpper(w[0]))) continue;
            var conf = ScoreCandidate(lines, i);
            if (conf > bestConf) { best = string.Join(" ", words); bestConf = conf; }
        }
        return (best, bestConf);
    }

    private static string StripNonNameTail(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return line;
        var cuts = new List<int>();
        var dm = DateRegex().Match(line);
        if (dm.Success) cuts.Add(dm.Index);
        var lower = line.ToLowerInvariant();
        foreach (var a in FieldAnchors)
        {
            var idx = lower.IndexOf(a, StringComparison.Ordinal);
            if (idx > 0) cuts.Add(idx);
        }
        return cuts.Count == 0 ? line : line[..cuts.Min()].Trim();
    }

    private static float ScoreCandidate(string[] lines, int idx)
    {
        var score = 0.5f;
        if (idx < 10) score += 0.1f;
        var near = 0;
        for (var j = Math.Max(0, idx - 3); j < Math.Min(lines.Length, idx + 4); j++)
            if (j != idx && IsFieldAnchor(lines[j])) near++;
        if (near >= 1) score += 0.15f;
        if (near >= 2) score += 0.10f;
        for (var j = Math.Max(0, idx - 5); j < Math.Min(lines.Length, idx + 6); j++)
            if (j != idx && CnpRegex().IsMatch(lines[j])) { score += 0.15f; break; }
        return Math.Min(1.0f, score);
    }

    private static bool IsFieldAnchor(string line) =>
        FieldAnchors.Any(a => line.ToLowerInvariant().Trim().StartsWith(a, StringComparison.Ordinal));

    private static string CleanNameValue(string raw)
    {
        var cleaned = InlineFieldRegex().Replace(raw, "").Trim();
        cleaned = Regex.Replace(cleaned, @"_+", "").Trim();
        cleaned = DateRegex().Replace(cleaned, "").Trim();
        cleaned = PurelyNumericRegex().Replace(cleaned, "").Trim();
        return cleaned.TrimEnd(':', '-', '–', ' ');
    }

    private static bool IsPlausibleName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !NameCandidateRegex().IsMatch(name)) return false;
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || words.Length > 5) return false;
        return words.Any(w => w.Length > 0 && char.IsUpper(w[0])) && !IsFieldAnchor(name);
    }
}

/// <summary>
/// Validates extracted identity against the logged-in patient (MAUI parity: name and CNP required on document,
/// CNP exact match, name fuzzy match).
/// </summary>
public sealed class DocumentIdentityValidator
{
    private readonly NameMatchingService _nameMatcher = new();

    public Models.IdentityValidationResult Validate(
        Models.DocumentIdentity identity,
        string? patientName,
        string? patientCnp)
    {
        if (string.IsNullOrWhiteSpace(identity.ExtractedName))
            return new Models.IdentityValidationResult(false, false, false, "No patient name found in document.");

        if (string.IsNullOrWhiteSpace(identity.ExtractedCnp))
            return new Models.IdentityValidationResult(false, false, false, "No CNP found in document.");

        var cnpMatched = !string.IsNullOrEmpty(patientCnp)
                         && string.Equals(identity.ExtractedCnp.Trim(), patientCnp.Trim(), StringComparison.Ordinal);

        if (!cnpMatched)
            return new Models.IdentityValidationResult(false, false, false, "CNP on the document does not match your profile.");

        var nameMatched = false;
        if (!string.IsNullOrEmpty(patientName) && !string.IsNullOrEmpty(identity.ExtractedName))
            nameMatched = _nameMatcher.Match(patientName, identity.ExtractedName).IsMatch;

        if (!nameMatched)
            return new Models.IdentityValidationResult(false, false, true, "Name on the document does not match your profile.");

        return new Models.IdentityValidationResult(true, true, true, null);
    }
}

/// <summary>
/// Extracts medical history items (medications) from sanitized OCR text.
/// </summary>
public sealed partial class MedicalHistoryExtractor
{
    [GeneratedRegex(@"^\s*(?:Rp\.?\s*:?\s*)?(?:\d+[\.\)]\s*)?(?<name>[A-Za-zĂÂÎȘȚăâîșț][\w\s\-]*?)\s+(?<dosage>\d+\s*(?:mg|g|mcg|ml)\b)(?<rest>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex MedicationLineRegex();

    [GeneratedRegex(@"^\s*(?:Rp\.?\s*:?\s*)?\d+[\.\)]", RegexOptions.IgnoreCase)]
    private static partial Regex EntryStartRegex();

    public IReadOnlyList<Models.ExtractedHistoryItem> Extract(string? sanitizedText)
    {
        if (string.IsNullOrWhiteSpace(sanitizedText)) return [];

        var rawLines = sanitizedText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var merged = MergeContinuationLines(rawLines);
        var items = new List<Models.ExtractedHistoryItem>();

        foreach (var line in merged)
        {
            var match = MedicationLineRegex().Match(line);
            if (!match.Success) continue;
            var name = match.Groups["name"].Value.Trim();
            var dosage = match.Groups["dosage"].Value.Trim();
            var rest = match.Groups["rest"].Value.Trim();
            var freq = ExtractFrequency(rest);
            var dur = ExtractDuration(rest);
            items.Add(new Models.ExtractedHistoryItem(
                Title: $"Prescription update: {name}",
                MedicationName: name, Dosage: dosage,
                Frequency: freq, Duration: dur, Notes: rest,
                Summary: $"{name} {dosage}, {freq}".TrimEnd(',', ' '),
                Confidence: EstimateConfidence(name, dosage, freq)));
        }
        return items;
    }

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

/// <summary>
/// Heuristic field extraction from OCR text.
/// </summary>
public sealed partial class HeuristicFieldExtractor
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

    public Models.HeuristicExtractionResult Extract(string? rawText, string docType)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return Models.HeuristicExtractionResult.Empty;

        var pid = ExtractField(CnpRegex(), rawText);
        var pname = ExtractField(NameAfterLabelRegex(), rawText);
        var date = ExtractField(DateRegex(), rawText);
        var doc = ExtractField(DoctorRegex(), rawText);
        var diag = (docType is "Referral" or "Discharge" or "ConsultationNote")
            ? ExtractField(DiagnosisRegex(), rawText) : null;
        var meds = docType == "Prescription" ? ExtractMedications(rawText) : [];

        return new Models.HeuristicExtractionResult(pname, pid, date, doc, diag, meds);
    }

    private static string? ExtractField(Regex regex, string text)
    {
        var m = regex.Match(text);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static List<Models.ExtractedMedicationField> ExtractMedications(string text)
    {
        var results = new List<Models.ExtractedMedicationField>();
        foreach (Match m in MedicationRegex().Matches(text))
            results.Add(new Models.ExtractedMedicationField(
                m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim(),
                null, m.Groups[3].Value.Trim()));
        return results;
    }
}
