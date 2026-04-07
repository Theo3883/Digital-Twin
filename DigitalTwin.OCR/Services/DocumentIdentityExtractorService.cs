using System.Text.RegularExpressions;
using DigitalTwin.Domain.Services;
using DigitalTwin.OCR.Models;
using DigitalTwin.OCR.Models.Graph;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Extracts patient identity (name and CNP) from raw OCR text.
/// Handles both labeled formats ("Nume: Popescu Ion") and unlabeled formats
/// where the name appears as a standalone line near known field anchors.
/// </summary>
public sealed partial class DocumentIdentityExtractorService
{
    private readonly AppDebugLogger<DocumentIdentityExtractorService>? _logger;

    public DocumentIdentityExtractorService() { }

    public DocumentIdentityExtractorService(AppDebugLogger<DocumentIdentityExtractorService> logger)
        => _logger = logger;

    [GeneratedRegex(@"\b[1-8]\d{12}\b")]
    private static partial Regex CnpRegex();

    // OCR digit confusions we commonly see in PDFs / screenshots.
    // Example: "504O30822672O" -> "5040308226720"
    private static readonly Dictionary<char, char> DigitConfusions = new()
    {
        ['O'] = '0', ['o'] = '0',
        ['I'] = '1', ['l'] = '1', ['|'] = '1',
        ['S'] = '5', ['s'] = '5',
        ['B'] = '8',
    };

    // Label-anchored CNP: "CNP" followed by digits (with possible OCR-introduced spaces/dots)
    [GeneratedRegex(@"(?:CNP)\s*[:\-–]?\s*([1-8][\d\s.]{11,15}\d)", RegexOptions.IgnoreCase)]
    private static partial Regex LabeledCnpRegex();

    // Space-tolerant: 13-digit-ish sequence starting with 1-8 that may contain internal spaces
    [GeneratedRegex(@"\b[1-8][\d\s]{12,16}\b")]
    private static partial Regex SpaceTolerantCnpRegex();

    // Match true "name" fields at the start of a line (multiline),
    // so we don't accidentally match substrings like "Cod pacient".
    [GeneratedRegex(
        @"(?m)^\s*(?:Nume\s+pacient(?:ului)?|Nume(?:\s+(?:și|si)\s+prenume(?:le)?)?|Prenume|Pacient)\b\s*[:\-–]?\s*(?<name>.+)\s*$",
        RegexOptions.IgnoreCase)]
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

    // Common institution/department lines that are frequently mistaken for a name in unlabeled extraction.
    [GeneratedRegex(@"\b(arcadia|policlinic(?:a)?|spital(?:ul)?|laborator(?:ul)?|radiologie|centru(?:l)?|clinica|medicale)\b", RegexOptions.IgnoreCase)]
    private static partial Regex InstitutionLineRegex();

    /// <summary>
    /// Known Romanian medical document field labels that indicate a line is NOT a patient name.
    /// </summary>
    private static readonly string[] FieldAnchors =
    [
        "data", "sex", "cnp", "telefon", "tel", "medic", "diagnostic",
        "laborator", "nr", "cod", "adresa", "vârsta", "varsta", "grupa",
        "rezultat", "analiz", "probe", "recoltare", "validat", "data nasterii",
        "data nașterii", "sectie", "secția", "spital", "clinica",
        "locatie", "locație"
    ];

    /// <summary>
    /// Alphabetic characters valid in Romanian names (letters, hyphens, spaces, diacritics).
    /// </summary>
    [GeneratedRegex(@"^[A-Za-zĂÂÎȘȚăâîșțÁÉÍÓÚáéíóú\-\s]+$")]
    private static partial Regex NameCandidateRegex();

    /// <summary>
    /// Extracts name and CNP from the given raw OCR text.
    /// </summary>
    public DocumentIdentity Extract(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return new DocumentIdentity(null, null, 0f, 0f);

        _logger?.Debug("[OCR Identity] RawText snippet ({Len} chars): {Snippet}",
            rawText.Length, rawText[..Math.Min(200, rawText.Length)].Replace('\n', ' '));

        var (cnp, cnpStrategy) = ExtractCnpWithFallbacks(rawText);
        var (name, nameConfidence) = ExtractName(rawText);

        var cnpConfidence = cnp is not null ? 1.0f : 0f;

        _logger?.Debug("[OCR Identity] CNP extraction: Found={Cnp} Strategy={Strategy}",
            cnp ?? "(null)", cnpStrategy);
        _logger?.Debug("[OCR Identity] Name extraction: Found={Name} Conf={Conf:F2}",
            name ?? "(null)", nameConfidence);

        var identity = new DocumentIdentity(name, cnp, nameConfidence, cnpConfidence);
        _logger?.Info("[OCR Identity] Result: Name={Name} CNP={Cnp} NameConf={NC:F2} CnpConf={CC:F2}",
            identity.ExtractedName ?? "(null)",
            identity.ExtractedCnp ?? "(null)",
            identity.NameConfidence,
            identity.CnpConfidence);

        return identity;
    }

    /// <summary>
    /// Extracts identity using both raw text and (optionally) the OCR graph tokens.
    /// The graph path is more robust for CNP because digits may be split across tokens/lines.
    /// </summary>
    public DocumentIdentity Extract(string rawText, OcrDocumentGraph? graph)
    {
        if (graph is null)
            return Extract(rawText);

        var graphText = graph.RawText;
        var mergedText = string.Join("\n", rawText, graphText);

        _logger?.Debug("[OCR Identity] Graph tokens available: {TokenCount}", graph.AllTokens.Count);

        var (cnp, cnpStrategy) = ExtractCnpWithGraphFallbacks(mergedText, graph);
        var (name, nameConfidence) = ExtractName(mergedText);

        var cnpConfidence = cnp is not null ? 1.0f : 0f;

        _logger?.Debug("[OCR Identity] CNP extraction: Found={Cnp} Strategy={Strategy}",
            cnp ?? "(null)", cnpStrategy);
        _logger?.Debug("[OCR Identity] Name extraction: Found={Name} Conf={Conf:F2}",
            name ?? "(null)", nameConfidence);

        return new DocumentIdentity(name, cnp, nameConfidence, cnpConfidence);
    }

    /// <summary>
    /// Extracts CNP with multiple fallback strategies to handle OCR artifacts.
    /// </summary>
    private static (string? Cnp, string Strategy) ExtractCnpWithFallbacks(string text)
    {
        // Strategy 1: Exact 13-digit match (highest confidence, no artifacts)
        var match = CnpRegex().Match(text);
        if (match.Success)
            return (match.Value, "exact");

        // Strategy 1b: Exact after OCR digit confusion normalization
        var normalized = NormalizeDigits(text);
        match = CnpRegex().Match(normalized);
        if (match.Success)
            return (match.Value, "exact-normalized");

        // Strategy 2: Label-anchored — "CNP" label followed by digits with possible spaces/dots
        var labelMatch = LabeledCnpRegex().Match(text);
        if (labelMatch.Success)
        {
            var candidate = Regex.Replace(labelMatch.Groups[1].Value, @"[\s.]", "");
            if (candidate.Length == 13 && candidate.All(char.IsDigit))
                return (candidate, "label-anchored");
        }

        // Strategy 3: Space-tolerant — 13+ chars of digits/spaces starting with 1-8
        foreach (Match stMatch in SpaceTolerantCnpRegex().Matches(text))
        {
            var candidate = Regex.Replace(stMatch.Value, @"\s", "");
            if (candidate.Length == 13 && candidate.All(char.IsDigit) && candidate[0] >= '1' && candidate[0] <= '8')
                return (candidate, "space-tolerant");
        }

        // Strategy 3b: Space-tolerant on normalized text
        foreach (Match stMatch in SpaceTolerantCnpRegex().Matches(normalized))
        {
            var candidate = Regex.Replace(stMatch.Value, @"\s", "");
            if (candidate.Length == 13 && candidate.All(char.IsDigit) && candidate[0] >= '1' && candidate[0] <= '8')
                return (candidate, "space-tolerant-normalized");
        }

        return (null, "none");
    }

    private static (string? Cnp, string Strategy) ExtractCnpWithGraphFallbacks(string mergedText, OcrDocumentGraph graph)
    {
        // Try existing strategies first.
        var baseResult = ExtractCnpWithFallbacks(mergedText);
        if (baseResult.Cnp is not null)
            return baseResult;

        // Strategy 4: Token-stream reconstruction — join digit-like tokens and scan for 13-digit sequences.
        // This helps when OCR splits digits across words/columns.
        var tokenText = string.Join(" ", graph.AllTokens.Select(t => t.Text));
        var normalized = NormalizeDigits(tokenText);

        // Remove anything that's not digit or whitespace, then scan for 13-digit runs.
        var digitsOnly = Regex.Replace(normalized, @"[^\d\s]", " ");
        foreach (var chunk in digitsOnly.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (chunk.Length != 13) continue;
            if (chunk[0] < '1' || chunk[0] > '8') continue;
            if (!chunk.All(char.IsDigit)) continue;
            return (chunk, "token-stream");
        }

        // Strategy 5: Sliding window across all digits in order (drops separators entirely).
        var justDigits = new string(digitsOnly.Where(char.IsDigit).ToArray());
        for (var i = 0; i + 13 <= justDigits.Length; i++)
        {
            var candidate = justDigits.Substring(i, 13);
            if (candidate[0] < '1' || candidate[0] > '8') continue;
            return (candidate, "token-stream-window");
        }

        return (null, "none");
    }

    private static string NormalizeDigits(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var chars = input.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (DigitConfusions.TryGetValue(chars[i], out var mapped))
                chars[i] = mapped;
        }
        return new string(chars);
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
            line = StripNonNameTail(line);

            // Skip empty, known labels, dates, phone numbers, CNP, purely numeric
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (IsFieldAnchor(line)) continue;
            if (InstitutionLineRegex().IsMatch(line)) continue;
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

    private static string StripNonNameTail(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return line;

        // If OCR put multiple fields on one line (common in screenshots),
        // take the prefix before the first obvious non-name indicator.
        // Examples:
        //   "Sandu Theodor Data nașterii 08.03.2004" -> "Sandu Theodor"
        //   "Sandu Theodor CNP 5040308226720"       -> "Sandu Theodor"
        var cutPoints = new List<int>();

        var dateMatch = DateRegex().Match(line);
        if (dateMatch.Success)
            cutPoints.Add(dateMatch.Index);

        var lower = line.ToLowerInvariant();
        foreach (var anchor in FieldAnchors)
        {
            var idx = lower.IndexOf(anchor, StringComparison.Ordinal);
            if (idx > 0)
                cutPoints.Add(idx);
        }

        if (cutPoints.Count == 0)
            return line;

        var cut = cutPoints.Min();
        return line.Substring(0, cut).Trim();
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
        if (!NameCandidateRegex().IsMatch(name)) return false;
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length < 2 || words.Length > 5) return false;
        if (!words.Any(w => w.Length > 0 && char.IsUpper(w[0]))) return false;
        if (IsFieldAnchor(name)) return false;
        return true;
    }
}
