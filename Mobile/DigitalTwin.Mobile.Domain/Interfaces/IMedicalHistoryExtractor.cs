using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface IMedicalHistoryExtractor
{
    IReadOnlyList<ExtractedHistoryItem> Extract(string? sanitizedText);
    IReadOnlyList<ExtractedHistoryItem> Extract(string? sanitizedText, string docType);
}
