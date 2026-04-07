namespace DigitalTwin.OCR.Models.Structured;

public enum ExtractionMethod
{
    HeuristicRegex,
    MlBertTokenClassifier,
    MlNlClassifier,
    BoundingBoxAlignment,
    Combined
}
