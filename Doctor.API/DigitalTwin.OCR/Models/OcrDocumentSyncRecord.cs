namespace DigitalTwin.OCR.Models;

/// <summary>Projection of an OCR result safe for SQLite persistence and cloud sync.</summary>
public record OcrDocumentSyncRecord(
    Guid Id,
    Guid PatientId,
    string OpaqueInternalName,
    string MimeType,
    int PageCount,
    string Sha256OfNormalized,
    string SanitizedOcrPreview,
    string EncryptedVaultPath,
    DateTime ScannedAt);
