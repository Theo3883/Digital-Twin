using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface IHeuristicFieldExtractor
{
    HeuristicExtractionResult Extract(string? rawText, string docType);
}
