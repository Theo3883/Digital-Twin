using DigitalTwin.OCR.Models.Enums;

namespace DigitalTwin.OCR.Models;

public record OcrSessionRequest(
    DocumentSourceType Source,
    Guid PatientId,
    SecurityMode SecurityMode = SecurityMode.Strict);
