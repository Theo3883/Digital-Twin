using System.Globalization;
using System.Text;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Fuzzy name matching with Romanian diacritics normalization and token-level Levenshtein distance.
/// Handles OCR misreads (e.g. "Teodor" vs "Theodor") and reversed name order.
/// </summary>
public sealed class NameMatchingService
{
    /// <summary>Maximum Levenshtein distance per token to still count as a match.</summary>
    private const int MaxDistancePerToken = 2;

    /// <summary>
    /// Compares two names with fuzzy matching. Returns true if the names are close enough
    /// after normalization, diacritics removal, and sorted token-level comparison.
    /// </summary>
    public NameMatchResult Match(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
            return new NameMatchResult(false, int.MaxValue, Normalize(expected ?? ""), Normalize(actual ?? ""));

        var normalizedExpected = Normalize(expected);
        var normalizedActual = Normalize(actual);

        // Exact match after normalization
        if (normalizedExpected == normalizedActual)
            return new NameMatchResult(true, 0, normalizedExpected, normalizedActual);

        var expectedTokens = Tokenize(normalizedExpected);
        var actualTokens = Tokenize(normalizedActual);

        // Try sorted token-by-token comparison (handles reversed order)
        var sortedResult = CompareTokensSorted(expectedTokens, actualTokens);
        if (sortedResult.IsMatch)
            return sortedResult with { NormalizedExpected = normalizedExpected, NormalizedActual = normalizedActual };

        // Try subset matching: all expected tokens appear in actual (document may have middle name)
        var subsetResult = CompareTokensSubset(expectedTokens, actualTokens);
        return subsetResult with { NormalizedExpected = normalizedExpected, NormalizedActual = normalizedActual };
    }

    private static NameMatchResult CompareTokensSorted(string[] expected, string[] actual)
    {
        var sortedExpected = expected.OrderBy(t => t, StringComparer.Ordinal).ToArray();
        var sortedActual = actual.OrderBy(t => t, StringComparer.Ordinal).ToArray();

        if (sortedExpected.Length != sortedActual.Length)
            return new NameMatchResult(false, int.MaxValue, "", "");

        var totalDistance = 0;
        for (var i = 0; i < sortedExpected.Length; i++)
        {
            var dist = LevenshteinDistance(sortedExpected[i], sortedActual[i]);
            if (dist > MaxDistancePerToken)
                return new NameMatchResult(false, int.MaxValue, "", "");
            totalDistance += dist;
        }

        return new NameMatchResult(true, totalDistance, "", "");
    }

    private static NameMatchResult CompareTokensSubset(string[] expected, string[] actual)
    {
        // Check if every expected token has a close match in actual (handles extra middle names)
        if (actual.Length < expected.Length)
            return new NameMatchResult(false, int.MaxValue, "", "");

        var usedIndices = new HashSet<int>();
        var totalDistance = 0;

        foreach (var expToken in expected)
        {
            var bestDist = int.MaxValue;
            var bestIdx = -1;

            for (var j = 0; j < actual.Length; j++)
            {
                if (usedIndices.Contains(j)) continue;
                var dist = LevenshteinDistance(expToken, actual[j]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = j;
                }
            }

            if (bestIdx < 0 || bestDist > MaxDistancePerToken)
                return new NameMatchResult(false, int.MaxValue, "", "");

            usedIndices.Add(bestIdx);
            totalDistance += bestDist;
        }

        return new NameMatchResult(true, totalDistance, "", "");
    }

    internal static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        // Lowercase and strip Romanian diacritics
        var lower = name.ToLowerInvariant();
        return StripDiacritics(lower).Trim();
    }

    private static string[] Tokenize(string normalizedName)
        => normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string StripDiacritics(string text)
    {
        // Fast path for common Romanian diacritics
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = chars[i] switch
            {
                'ă' or 'â' => 'a',
                'î' => 'i',
                'ș' => 's',
                'ț' => 't',
                'á' or 'à' or 'ä' => 'a',
                'é' or 'è' or 'ë' => 'e',
                'í' or 'ì' or 'ï' => 'i',
                'ó' or 'ò' or 'ö' => 'o',
                'ú' or 'ù' or 'ü' => 'u',
                _ => chars[i]
            };
        }

        // Fallback: use Unicode normalization for any remaining combining marks
        var result = new string(chars);
        var normalized = result.Normalize(NormalizationForm.FormD);
        var stripped = new char[normalized.Length];
        var idx = 0;
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                stripped[idx++] = c;
        }

        return new string(stripped, 0, idx);
    }

    internal static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        var n = s.Length;
        var m = t.Length;

        // Use single array optimization
        var prev = new int[m + 1];
        var curr = new int[m + 1];

        for (var j = 0; j <= m; j++)
            prev[j] = j;

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

/// <summary>Result of a fuzzy name comparison.</summary>
public sealed record NameMatchResult(
    bool IsMatch,
    int Distance,
    string NormalizedExpected,
    string NormalizedActual);
