namespace DigitalTwin.Infrastructure.Entities;

public class OcrDocumentEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public string OpaqueInternalName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string DocumentType { get; set; } = "Unknown";
    public int PageCount { get; set; }
    public string Sha256OfNormalized { get; set; } = string.Empty;
    public string SanitizedOcrPreview { get; set; } = string.Empty;

    /// <summary>Path inside the AES-GCM vault — local device only, never synced to cloud.</summary>
    public string EncryptedVaultPath { get; set; } = string.Empty;

    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Sync tracking ─────────────────────────────────────────────────────────
    public bool IsDirty { get; set; } = true;
    public DateTime? SyncedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
