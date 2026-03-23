namespace DigitalTwin.OCR.Models;

/// <summary>Describes an AES-GCM encrypted document at rest in the local vault.</summary>
public record EncryptedDocumentDescriptor(
    Guid DocumentId,
    string OpaqueInternalName,
    string VaultPath,
    string NonceB64,
    string TagB64,
    string WrappedDekB64,
    string Sha256OfNormalized,
    string MimeType,
    int PageCount,
    DateTime EncryptedAt);
