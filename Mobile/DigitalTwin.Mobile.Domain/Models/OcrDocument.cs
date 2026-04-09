namespace DigitalTwin.Mobile.Domain.Models;

public class OcrDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public string OpaqueInternalName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public string Sha256OfNormalized { get; set; } = string.Empty;
    public string SanitizedOcrPreview { get; set; } = string.Empty;
    public string EncryptedVaultPath { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDirty { get; set; } = true;
    public DateTime? SyncedAt { get; set; }
}
