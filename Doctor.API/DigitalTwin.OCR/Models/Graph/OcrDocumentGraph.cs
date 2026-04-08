namespace DigitalTwin.OCR.Models.Graph;

/// <summary>
/// Spatial graph of a fully OCR-processed document.
/// Additive output — the existing RawText path is unaffected.
/// </summary>
public sealed record OcrDocumentGraph(
    IReadOnlyList<OcrGraphPage> Pages,
    IReadOnlyList<OcrToken> AllTokens,
    string DetectedLanguage)
{
    public string RawText => string.Join("\n", AllTokens.Select(t => t.Text));

    public IEnumerable<OcrToken> TokensForPage(int pageIndex)
        => AllTokens.Where(t => t.PageIndex == pageIndex);

    public static OcrDocumentGraph Empty(string language) =>
        new([], [], language);
}
