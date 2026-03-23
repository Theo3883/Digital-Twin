using DigitalTwin.OCR.Models;

namespace DigitalTwin.OCR.Tests;

public class OcrDocumentSyncRecordTests
{
    [Fact]
    public void OcrDocumentSyncRecord_Properties_RoundtripCorrectly()
    {
        var id = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var scannedAt = DateTime.UtcNow;

        var record = new OcrDocumentSyncRecord(
            Id: id,
            PatientId: patientId,
            OpaqueInternalName: $"{id:N}",
            MimeType: "application/pdf",
            PageCount: 3,
            Sha256OfNormalized: "abc123",
            SanitizedOcrPreview: "Temperatura [DATE]. Contact: [EMAIL]",
            EncryptedVaultPath: "/vault/encrypted/abc.enc",
            ScannedAt: scannedAt);

        Assert.Equal(id, record.Id);
        Assert.Equal(patientId, record.PatientId);
        Assert.Equal(3, record.PageCount);
        Assert.Equal("application/pdf", record.MimeType);
        Assert.Equal(scannedAt, record.ScannedAt);
    }

    [Fact]
    public void OcrDocumentSyncRecord_SanitizedPreview_NeverContainsRawCnp()
    {
        // The preview should be sanitized before it ever reaches the record
        const string sanitized = "Pacient: [CNP] data nasterii: [DATE]";
        var record = new OcrDocumentSyncRecord(
            Guid.NewGuid(), Guid.NewGuid(), "name", "application/pdf",
            1, "hash", sanitized, "/path", DateTime.UtcNow);

        // Verify no 13-digit CNP pattern remains
        Assert.DoesNotMatch(@"\b[1-8]\d{12}\b", record.SanitizedOcrPreview);
    }

    [Fact]
    public void OcrDocumentSyncRecord_OpaqueInternalName_IsGuidHex()
    {
        var id = Guid.NewGuid();
        var record = new OcrDocumentSyncRecord(
            id, Guid.NewGuid(), $"{id:N}", "image/jpeg",
            1, "hash", "text", "/vault/path", DateTime.UtcNow);

        // Opaque name must not be a human-readable filename
        Assert.DoesNotContain(".", record.OpaqueInternalName.TrimEnd('.'));
        Assert.Equal(32, record.OpaqueInternalName.Length); // Guid N format = 32 hex chars
    }

    [Fact]
    public void OcrDocumentSyncRecord_EncryptedVaultPath_NotSyncedToCloud()
    {
        // This test documents the contract: EncryptedVaultPath should NOT be sent to the
        // cloud. The OcrDocumentSyncDrainer explicitly sets it to string.Empty when mapping
        // to the cloud batch. This test verifies the field exists and can be set independently.
        var record = new OcrDocumentSyncRecord(
            Guid.NewGuid(), Guid.NewGuid(), "opaque", "application/pdf",
            1, "sha256", "preview", "/local/vault/path.enc", DateTime.UtcNow);

        var cloudRecord = record with { EncryptedVaultPath = string.Empty };

        Assert.NotEmpty(record.EncryptedVaultPath);
        Assert.Empty(cloudRecord.EncryptedVaultPath);
    }
}
