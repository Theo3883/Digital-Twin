using System.Globalization;
using System.Text;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.OCR.Services;

/// <summary>
/// Name matching with Romanian diacritics normalization and Levenshtein distance.
/// </summary>
public sealed class NameMatchingService : INameMatchingService
{
    private const int MaxDistancePerToken = 2;

    public NameMatchResult Match(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
            return new NameMatchResult(false, int.MaxValue, Normalize(expected ?? ""), Normalize(actual ?? ""));

        var ne = Normalize(expected);
        var na = Normalize(actual);

        if (ne.Length == 0 || na.Length == 0)
            return new NameMatchResult(false, int.MaxValue, ne, na);

        if (ne == na) return new NameMatchResult(true, 0, ne, na);

        var et = Tokenize(ne);
        var at = Tokenize(na);

        if (et.Length == 0 || at.Length == 0)
            return new NameMatchResult(false, int.MaxValue, ne, na);

        var sorted = CompareTokensSorted(et, at);
        if (sorted.IsMatch) return sorted with { NormalizedExpected = ne, NormalizedActual = na };

        var subsetForward = CompareTokensSubset(et, at);
        if (subsetForward.IsMatch)
            return subsetForward with { NormalizedExpected = ne, NormalizedActual = na };

        var subsetReverse = CompareTokensSubset(at, et);
        return subsetReverse with { NormalizedExpected = ne, NormalizedActual = na };
    }

    private static NameMatchResult CompareTokensSorted(string[] expected, string[] actual)
    {
        var se = expected.OrderBy(t => t, StringComparer.Ordinal).ToArray();
        var sa = actual.OrderBy(t => t, StringComparer.Ordinal).ToArray();
        if (se.Length != sa.Length) return new NameMatchResult(false, int.MaxValue, "", "");

        var total = 0;
        for (var i = 0; i < se.Length; i++)
        {
            var d = LevenshteinDistance(se[i], sa[i]);
            if (d > MaxDistancePerToken) return new NameMatchResult(false, int.MaxValue, "", "");
            total += d;
        }
        return new NameMatchResult(true, total, "", "");
    }

    private static NameMatchResult CompareTokensSubset(string[] expected, string[] actual)
    {
        if (actual.Length < expected.Length) return new NameMatchResult(false, int.MaxValue, "", "");
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
            if (bi < 0 || best > MaxDistancePerToken) return new NameMatchResult(false, int.MaxValue, "", "");
            used.Add(bi); total += best;
        }
        return new NameMatchResult(true, total, "", "");
    }

    internal static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var lower = StripDiacritics(name.ToLowerInvariant());

        // Keep only letters and normalize any separator/punctuation to a single space.
        var sb = new StringBuilder(lower.Length);
        var lastWasSpace = true;
        foreach (var c in lower)
        {
            if (char.IsLetter(c))
            {
                sb.Append(c);
                lastWasSpace = false;
                continue;
            }

            if (!lastWasSpace)
            {
                sb.Append(' ');
                lastWasSpace = true;
            }
        }

        return sb.ToString().Trim();
    }

    private static string[] Tokenize(string n) =>
        n.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string StripDiacritics(string text)
    {
        // Fast path for common Romanian diacritics and frequent accented OCR variants.
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

        // Fallback for any remaining combining marks.
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
