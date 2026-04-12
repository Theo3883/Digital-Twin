using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface IDocumentIdentityExtractor
{
    DocumentIdentity Extract(string rawText);
}
