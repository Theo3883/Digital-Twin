using DigitalTwin.OCR.Models.Graph;
using DigitalTwin.OCR.Models.Structured;

namespace DigitalTwin.OCR.Services.Extraction;

/// <summary>
/// Extracts lab result rows from an OcrDocumentGraph using geometric bounding-box alignment.
/// No ML required — clusters tokens by Y-coordinate into rows, identifies column boundaries
/// from Romanian header keywords, then maps data tokens to columns.
/// </summary>
public sealed class GeometricTableExtractor
{
    // Known Romanian lab result column header tokens (upper-case)
    private static readonly string[] AnalysisHeaders   = ["ANALIZA", "TEST", "DENUMIRE", "ANALIZE"];
    private static readonly string[] ResultHeaders     = ["REZULTAT", "VALOARE"];
    private static readonly string[] UnitHeaders       = ["UNITATE", "U.M.", "UM"];
    private static readonly string[] ReferenceHeaders  = ["REFERINTA", "REFERINȚĂ", "REF", "VALORI"];

    private const float RowToleranceY = 0.012f; // fraction of page height for same-row clustering

    public IReadOnlyList<ExtractedLabResult> Extract(OcrDocumentGraph graph)
    {
        var results = new List<ExtractedLabResult>();

        foreach (var page in graph.Pages)
        {
            var pageResults = ExtractFromPage(page);
            results.AddRange(pageResults);
        }

        return results;
    }

    private static IReadOnlyList<ExtractedLabResult> ExtractFromPage(OcrGraphPage page)
    {
        if (page.Tokens.Count == 0) return [];

        // 1. Cluster tokens into rows by CenterY
        var rows = ClusterIntoRows(page.Tokens);

        // 2. Find the header row
        int headerRowIdx = FindHeaderRow(rows);
        if (headerRowIdx < 0) return [];

        // 3. Determine column X-ranges from header tokens
        var columns = DetermineColumns(rows[headerRowIdx]);
        if (columns.AnalysisRange is null || columns.ResultRange is null) return [];

        // 4. Parse data rows below the header
        var labResults = new List<ExtractedLabResult>();
        for (int i = headerRowIdx + 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var labResult = ParseDataRow(row, columns);
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

        // Sort tokens within each row by X
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
            bool hasResult   = ResultHeaders.Any(h => rowText.Contains(h));
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

            if (columns.AnalysisRange is { } ar && xScaled >= ar.Start.Value && xScaled <= ar.End.Value)
                analysisName = (analysisName is null) ? token.Text : $"{analysisName} {token.Text}";
            else if (columns.ResultRange is { } rr && xScaled >= rr.Start.Value && xScaled <= rr.End.Value)
                resultValue = (resultValue is null) ? token.Text : $"{resultValue} {token.Text}";
            else if (columns.UnitRange is { } ur && xScaled >= ur.Start.Value && xScaled <= ur.End.Value)
                unit = (unit is null) ? token.Text : $"{unit} {token.Text}";
            else if (columns.ReferenceRange is { } refR && xScaled >= refR.Start.Value && xScaled <= refR.End.Value)
                referenceRange = (referenceRange is null) ? token.Text : $"{referenceRange} {token.Text}";
        }

        if (string.IsNullOrWhiteSpace(analysisName) || string.IsNullOrWhiteSpace(resultValue))
            return null;

        bool outOfRange = IsOutOfRange(resultValue, referenceRange);

        return new ExtractedLabResult(
            AnalysisName: new ExtractedField<string>(analysisName.Trim(), 0.85f, ExtractionMethod.BoundingBoxAlignment),
            Value: new ExtractedField<string>(resultValue.Trim(), 0.85f, ExtractionMethod.BoundingBoxAlignment),
            Unit: unit is null ? null : new ExtractedField<string>(unit.Trim(), 0.80f, ExtractionMethod.BoundingBoxAlignment),
            ReferenceRange: referenceRange is null ? null : new ExtractedField<string>(referenceRange.Trim(), 0.80f, ExtractionMethod.BoundingBoxAlignment),
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

        // Expects "lo–hi" or "lo-hi" format
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
