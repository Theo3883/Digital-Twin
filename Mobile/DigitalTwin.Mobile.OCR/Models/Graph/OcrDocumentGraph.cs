namespace DigitalTwin.Mobile.OCR.Models.Graph;

public sealed record OcrDocumentGraph(
    IReadOnlyList<OcrGraphPage> Pages,
    IReadOnlyList<OcrToken> AllTokens,
    string DetectedLanguage)
{
    public string RawText => string.Join("\n", AllTokens.Select(t => t.Text));

    public IEnumerable<OcrToken> TokensForPage(int pageIndex)
        => AllTokens.Where(t => t.PageIndex == pageIndex);
}
