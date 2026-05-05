namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface IDocumentTypeClassifier
{
    string Classify(string? ocrText);
}
