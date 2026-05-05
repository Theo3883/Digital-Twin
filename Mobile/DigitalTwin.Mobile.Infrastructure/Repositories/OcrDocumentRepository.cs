using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace DigitalTwin.Mobile.Infrastructure.Repositories;

public class OcrDocumentRepository : IOcrDocumentRepository
{
    private readonly SqliteConnectionFactory _db;

    public OcrDocumentRepository(SqliteConnectionFactory db) => _db = db;

    public async Task<IEnumerable<OcrDocument>> GetByPatientIdAsync(Guid patientId)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM OcrDocuments WHERE PatientId = @pid ORDER BY ScannedAt DESC";
        cmd.Parameters.AddWithValue("@pid", patientId.ToString());
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<OcrDocument>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));
        return list;
    }

    public async Task<OcrDocument?> GetByIdAsync(Guid id)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM OcrDocuments WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadEntity(r) : null;
    }

    public async Task SaveAsync(OcrDocument document)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO OcrDocuments
                (Id, PatientId, OpaqueInternalName, MimeType, DocumentType, PageCount, Sha256OfNormalized,
                 SanitizedOcrPreview, EncryptedVaultPath, ScannedAt, CreatedAt, UpdatedAt, IsDirty, SyncedAt)
            VALUES
                (@Id, @PatientId, @OpaqueInternalName, @MimeType, @DocumentType, @PageCount, @Sha256OfNormalized,
                 @SanitizedOcrPreview, @EncryptedVaultPath, @ScannedAt, @CreatedAt, @UpdatedAt, @IsDirty, @SyncedAt)
            """;
        AddParams(cmd, document);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(OcrDocument document)
    {
        await SaveAsync(document);
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM OcrDocuments WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<OcrDocument>> GetDirtyAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM OcrDocuments WHERE IsDirty = 1 ORDER BY ScannedAt";
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<OcrDocument>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));
        return list;
    }

    public async Task MarkSyncedAsync(Guid id)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE OcrDocuments SET IsDirty = 0, SyncedAt = @now WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    private static void AddParams(SqliteCommand cmd, OcrDocument d)
    {
        cmd.Parameters.AddWithValue("@Id", d.Id.ToString());
        cmd.Parameters.AddWithValue("@PatientId", d.PatientId.ToString());
        cmd.Parameters.AddWithValue("@OpaqueInternalName", d.OpaqueInternalName);
        cmd.Parameters.AddWithValue("@MimeType", (object?)d.MimeType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DocumentType", string.IsNullOrWhiteSpace(d.DocumentType) ? "Unknown" : d.DocumentType);
        cmd.Parameters.AddWithValue("@PageCount", d.PageCount);
        cmd.Parameters.AddWithValue("@Sha256OfNormalized", (object?)d.Sha256OfNormalized ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SanitizedOcrPreview", (object?)d.SanitizedOcrPreview ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EncryptedVaultPath", (object?)d.EncryptedVaultPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ScannedAt", d.ScannedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@CreatedAt", d.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@UpdatedAt", d.UpdatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@IsDirty", d.IsDirty ? 1L : 0L);
        cmd.Parameters.AddWithValue("@SyncedAt", d.SyncedAt.HasValue ? d.SyncedAt.Value.ToString("O") : DBNull.Value);
    }

    private static OcrDocument ReadEntity(SqliteDataReader r) => new()
    {
        Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
        PatientId = Guid.Parse(r.GetString(r.GetOrdinal("PatientId"))),
        OpaqueInternalName = r.GetString(r.GetOrdinal("OpaqueInternalName")),
        MimeType = r.IsDBNull(r.GetOrdinal("MimeType")) ? string.Empty : r.GetString(r.GetOrdinal("MimeType")),
        DocumentType = r.IsDBNull(r.GetOrdinal("DocumentType")) ? "Unknown" : r.GetString(r.GetOrdinal("DocumentType")),
        PageCount = r.GetInt32(r.GetOrdinal("PageCount")),
        Sha256OfNormalized = r.IsDBNull(r.GetOrdinal("Sha256OfNormalized")) ? string.Empty : r.GetString(r.GetOrdinal("Sha256OfNormalized")),
        SanitizedOcrPreview = r.IsDBNull(r.GetOrdinal("SanitizedOcrPreview")) ? string.Empty : r.GetString(r.GetOrdinal("SanitizedOcrPreview")),
        EncryptedVaultPath = r.IsDBNull(r.GetOrdinal("EncryptedVaultPath")) ? string.Empty : r.GetString(r.GetOrdinal("EncryptedVaultPath")),
        ScannedAt = DateTime.Parse(r.GetString(r.GetOrdinal("ScannedAt"))),
        CreatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
        UpdatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("UpdatedAt"))),
        IsDirty = r.GetInt64(r.GetOrdinal("IsDirty")) != 0,
        SyncedAt = r.IsDBNull(r.GetOrdinal("SyncedAt")) ? null : DateTime.Parse(r.GetString(r.GetOrdinal("SyncedAt")))
    };
}
