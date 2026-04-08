using DigitalTwin.OCR.Models.Enums;

namespace DigitalTwin.OCR.Models;

public enum OcrSessionStatus { Success, Error, Cancelled }

public record OcrSessionResult(
    OcrSessionStatus Status,
    Guid? DocumentId = null,
    string? SanitizedPreview = null,
    OcrExecutionStatus OcrStatus = OcrExecutionStatus.Failed,
    string? SafeErrorMessage = null);
