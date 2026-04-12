namespace DigitalTwin.Mobile.OCR.Models.Structured;

public enum ExtractionMethod
{
    HeuristicRegex,
    MlBertTokenClassifier,
    MlNlClassifier,
    BoundingBoxAlignment,
    Combined
}
