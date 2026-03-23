namespace DigitalTwin.Domain.Models;

/// <summary>
/// Represents an OCR-processed medical document stored locally and synced to the cloud.
/// Only metadata and sanitized content reach the cloud — the encrypted vault path stays local.
/// </summary>
public class OcrDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }

    /// <summary>Opaque internal name generated at scan time — the original filename is never stored.</summary>
    public string OpaqueInternalName { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;
    public int PageCount { get; set; }

    /// <summary>SHA-256 of the normalised (pre-encryption) document bytes for integrity verification.</summary>
    public string Sha256OfNormalized { get; set; } = string.Empty;

    /// <summary>Regex-redacted OCR text safe for cloud sync and doctor portal display.</summary>
    public string SanitizedOcrPreview { get; set; } = string.Empty;

    /// <summary>Path inside the local AES-GCM vault. Never synced to the cloud.</summary>
    public string EncryptedVaultPath { get; set; } = string.Empty;

    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Sync tracking (same pattern as all other domain models) ──────────────
    public bool IsDirty { get; set; } = true;
    public DateTime? SyncedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
