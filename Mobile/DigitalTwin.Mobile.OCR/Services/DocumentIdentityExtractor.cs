using System.Text.RegularExpressions;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.OCR.Services;

/// <summary>
/// Extracts patient identity (name and CNP) from raw OCR text.
/// </summary>
public sealed partial class DocumentIdentityExtractor : IDocumentIdentityExtractor
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

    public DocumentIdentity Extract(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return new DocumentIdentity(null, null, 0f, 0f);

        var (cnp, _) = ExtractCnpWithFallbacks(rawText);
        var (name, nameConfidence) = ExtractName(rawText);
        var cnpConfidence = cnp is not null ? 1.0f : 0f;

        return new DocumentIdentity(name, cnp, nameConfidence, cnpConfidence);
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
