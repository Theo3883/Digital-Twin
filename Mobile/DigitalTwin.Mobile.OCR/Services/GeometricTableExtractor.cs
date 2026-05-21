using System.Globalization;
using System.Text.RegularExpressions;
using DigitalTwin.Mobile.OCR.Models.Graph;
using DigitalTwin.Mobile.OCR.Models.Structured;

namespace DigitalTwin.Mobile.OCR.Services;

/// <summary>
/// Extracts lab result rows from an OcrDocumentGraph using geometric bounding-box alignment.
/// No ML required — clusters tokens by Y-coordinate into rows, identifies column boundaries
/// from Romanian header keywords, then maps data tokens to columns.
/// </summary>
public sealed class GeometricTableExtractor
{
    private static readonly string[] AnalysisHeaders = ["ANALIZA", "TEST", "DENUMIRE", "ANALIZE"];
    private static readonly string[] ResultHeaders = ["REZULTAT", "VALOARE"];
    private static readonly string[] UnitHeaders = ["UNITATE", "U.M.", "UM"];
    private static readonly string[] ReferenceHeaders = ["REFERINTA", "REFERINȚĂ", "REF", "VALORI"];

    private const float RowToleranceY = 0.012f;

    private static string CleanTokenText(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s ?? string.Empty;
        // Remove obvious OCR artefacts
        s = s.Replace('|', ' ').Replace('\u2013', '-').Replace('\u2014', '-');
        // Remove stray non-printable/control chars
        s = Regex.Replace(s, "[\u0000-\u001F]+", " ");
        // Collapse multiple spaces
        s = Regex.Replace(s, "\\s+", " ").Trim();
        return s;
    }

    // Try to split combined strings like "13.4 g/dL 11.5 - 17.5" into value, unit, reference
    private static (string Value, string? Unit, string? Reference) NormalizeValueUnitReference(string? rawValue, string? rawUnit, string? rawReference)
    {
        rawValue = CleanTokenText(rawValue ?? string.Empty);
        rawUnit = CleanTokenText(rawUnit ?? string.Empty);
        rawReference = CleanTokenText(rawReference ?? string.Empty);

        string? value = null;
        string? unit = string.IsNullOrWhiteSpace(rawUnit) ? null : rawUnit;
        string? reference = string.IsNullOrWhiteSpace(rawReference) ? null : rawReference;

        // If rawValue contains a leading numeric token, capture it
        var m = Regex.Match(rawValue, "([+-]?[0-9]+(?:[\\.,][0-9]+)?)");
        if (m.Success)
        {
            value = m.Groups[1].Value.Replace(',', '.');
            // remainder after the matched number may contain unit/reference
            var rem = rawValue.Substring(m.Index + m.Length).Trim();
            if (!string.IsNullOrWhiteSpace(rem))
            {
                // if we don't already have unit, try to take letters portion as unit
                if (unit is null)
                {
                    // split rem into unit and possible reference range by finding a numeric range
                    var rangeMatch = Regex.Match(rem, "([0-9]{1,3}(?:[\\.,][0-9]+)?\\s*[-–]\\s*[0-9]{1,3}(?:[\\.,][0-9]+)?)");
                    if (rangeMatch.Success)
                    {
                        reference = rangeMatch.Groups[1].Value.Replace(',', '.');
                        unit = rem.Substring(0, rangeMatch.Index).Trim();
                    }
                    else
                    {
                        unit = rem.Trim();
                    }
                }
                else
                {
                    // unit already present; if remainder contains range and reference empty, set it
                    if (reference is null)
                    {
                        var rangeMatch = Regex.Match(rem, "([0-9]{1,3}(?:[\\.,][0-9]+)?\\s*[-–]\\s*[0-9]{1,3}(?:[\\.,][0-9]+)?)");
                        if (rangeMatch.Success)
                            reference = rangeMatch.Groups[1].Value.Replace(',', '.');
                    }
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(rawValue))
        {
            // Nothing numeric in value cell; maybe unit cell contains the value+range
            var rangeMatch = Regex.Match(rawUnit ?? string.Empty, "([0-9]+(?:[\\.,][0-9]+)?(?:\\s*[-–]\\s*[0-9]+(?:[\\.,][0-9]+)?)?)");
            if (rangeMatch.Success)
            {
                value = rangeMatch.Groups[1].Value.Replace(',', '.');
                // remove from unit
                unit = Regex.Replace(rawUnit ?? string.Empty, Regex.Escape(rangeMatch.Groups[1].Value), "").Trim();
            }
            else
            {
                value = rawValue.Trim();
            }
        }

        // If unit contains a numeric range, split it
        if (!string.IsNullOrWhiteSpace(unit) && reference is null)
        {
            var rangeMatch2 = Regex.Match(unit, "([0-9]{1,3}(?:[\\.,][0-9]+)?\\s*[-–]\\s*[0-9]{1,3}(?:[\\.,][0-9]+)?)");
            if (rangeMatch2.Success)
            {
                reference = rangeMatch2.Groups[1].Value.Replace(',', '.');
                unit = Regex.Replace(unit, Regex.Escape(rangeMatch2.Groups[1].Value), "").Trim();
            }
        }

        // Final cleanups
        if (!string.IsNullOrWhiteSpace(value)) value = value.Trim();
        if (!string.IsNullOrWhiteSpace(unit)) unit = unit.Trim();
        if (!string.IsNullOrWhiteSpace(reference)) reference = reference.Trim();

        return (Value: value ?? string.Empty, Unit: unit, Reference: reference);
    }

    public IReadOnlyList<ExtractedLabResult> Extract(OcrDocumentGraph graph)
    {
        var results = new List<ExtractedLabResult>();
        foreach (var page in graph.Pages)
            results.AddRange(ExtractFromPage(page));
        return results;
    }

    private static IReadOnlyList<ExtractedLabResult> ExtractFromPage(OcrGraphPage page)
    {
        if (page.Tokens.Count == 0) return [];

        var rows = ClusterIntoRows(page.Tokens);
        int headerRowIdx = FindHeaderRow(rows);
        if (headerRowIdx < 0) return [];

        var columns = DetermineColumns(rows[headerRowIdx]);
        if (columns.AnalysisRange is null || columns.ResultRange is null) return [];

        var labResults = new List<ExtractedLabResult>();
        for (int i = headerRowIdx + 1; i < rows.Count; i++)
        {
            var labResult = ParseDataRow(rows[i], columns);
            if (labResult is not null)
                labResults.Add(labResult);
        }
        return labResults;
    }

    private static List<List<OcrToken>> ClusterIntoRows(IReadOnlyList<OcrToken> tokens)
    {
        var sorted = tokens.OrderBy(t => t.BoundingBox.CenterY).ToList();
        var rows = new List<List<OcrToken>>();
        List<OcrToken>? currentRow = null;
        float currentRowY = float.MinValue;

        foreach (var token in sorted)
        {
            if (currentRow is null || Math.Abs(token.BoundingBox.CenterY - currentRowY) > RowToleranceY)
            {
                currentRow = [];
                rows.Add(currentRow);
                currentRowY = token.BoundingBox.CenterY;
            }
            currentRow.Add(token);
        }

        foreach (var row in rows)
            row.Sort((a, b) => a.BoundingBox.X.CompareTo(b.BoundingBox.X));

        return rows;
    }

    private static int FindHeaderRow(List<List<OcrToken>> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            var rowText = string.Join(" ", rows[i].Select(t => t.Text)).ToUpperInvariant();
            bool hasAnalysis = AnalysisHeaders.Any(h => rowText.Contains(h));
            bool hasResult = ResultHeaders.Any(h => rowText.Contains(h));
            if (hasAnalysis && hasResult)
                return i;
        }
        return -1;
    }

    private static (Range? AnalysisRange, Range? ResultRange, Range? UnitRange, Range? ReferenceRange)
        DetermineColumns(List<OcrToken> headerRow)
    {
        Range? analysisRange = null, resultRange = null, unitRange = null, referenceRange = null;

        foreach (var token in headerRow)
        {
            var upper = token.Text.ToUpperInvariant();
            var bb = token.BoundingBox;
            var r = new Range((int)(bb.X * 10000), (int)(bb.Right * 10000));

            if (AnalysisHeaders.Any(h => upper.Contains(h)))
                analysisRange = r;
            else if (ResultHeaders.Any(h => upper.Contains(h)))
                resultRange = r;
            else if (UnitHeaders.Any(h => upper.Contains(h)))
                unitRange = r;
            else if (ReferenceHeaders.Any(h => upper.Contains(h)))
                referenceRange = r;
        }

        return (analysisRange, resultRange, unitRange, referenceRange);
    }

    private static ExtractedLabResult? ParseDataRow(
        List<OcrToken> row,
        (Range? AnalysisRange, Range? ResultRange, Range? UnitRange, Range? ReferenceRange) columns)
    {
        string? analysisName = null, resultValue = null, unit = null, referenceRange = null;

        foreach (var token in row)
        {
            int xScaled = (int)(token.BoundingBox.CenterX * 10000);
            var txt = CleanTokenText(token.Text);
            if (columns.AnalysisRange is { } ar && xScaled >= ar.Start.Value && xScaled <= ar.End.Value)
                analysisName = (analysisName is null) ? txt : $"{analysisName} {txt}";
            else if (columns.ResultRange is { } rr && xScaled >= rr.Start.Value && xScaled <= rr.End.Value)
                resultValue = (resultValue is null) ? txt : $"{resultValue} {txt}";
            else if (columns.UnitRange is { } ur && xScaled >= ur.Start.Value && xScaled <= ur.End.Value)
                unit = (unit is null) ? txt : $"{unit} {txt}";
            else if (columns.ReferenceRange is { } refR && xScaled >= refR.Start.Value && xScaled <= refR.End.Value)
                referenceRange = (referenceRange is null) ? txt : $"{referenceRange} {txt}";
        }

        if (string.IsNullOrWhiteSpace(analysisName) || string.IsNullOrWhiteSpace(resultValue))
            return null;

        var normalized = NormalizeValueUnitReference(resultValue, unit, referenceRange);
        var valueFinal = normalized.Value;
        var unitFinal = normalized.Unit;
        var referenceFinal = normalized.Reference;

        bool outOfRange = IsOutOfRange(valueFinal, referenceFinal);

        return new ExtractedLabResult(
            AnalysisName: new ExtractedField<string>(analysisName.Trim(), 0.85f, ExtractionMethod.BoundingBoxAlignment),
            Value: new ExtractedField<string>(valueFinal.Trim(), 0.85f, ExtractionMethod.BoundingBoxAlignment),
            Unit: unitFinal is null ? null : new ExtractedField<string>(unitFinal.Trim(), 0.80f, ExtractionMethod.BoundingBoxAlignment),
            ReferenceRange: referenceFinal is null ? null : new ExtractedField<string>(referenceFinal.Trim(), 0.80f, ExtractionMethod.BoundingBoxAlignment),
            SampleDate: null,
            IsOutOfRange: outOfRange);
    }

    private static bool IsOutOfRange(string value, string? referenceRange)
    {
        if (referenceRange is null) return false;
        if (!float.TryParse(value.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var numVal))
            return false;

        var sep = referenceRange.Contains('–') ? '–' : '-';
        var parts = referenceRange.Split(sep, 2);
        if (parts.Length != 2) return false;

        bool loOk = float.TryParse(parts[0].Trim().Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var lo);
        bool hiOk = float.TryParse(parts[1].Trim().Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var hi);

        return loOk && hiOk && (numVal < lo || numVal > hi);
    }
}
