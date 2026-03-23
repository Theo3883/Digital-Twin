using DigitalTwin.OCR.Models.Enums;

namespace DigitalTwin.OCR.Models;

public record OcrPage(
    int PageIndex,
    IReadOnlyList<OcrTextBlock> Blocks,
    OcrExecutionStatus Status,
    string? DetectedLanguage);
