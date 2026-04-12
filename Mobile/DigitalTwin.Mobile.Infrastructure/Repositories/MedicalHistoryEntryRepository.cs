using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace DigitalTwin.Mobile.Infrastructure.Repositories;

public class MedicalHistoryEntryRepository : IMedicalHistoryEntryRepository
{
    private readonly SqliteConnectionFactory _db;

    public MedicalHistoryEntryRepository(SqliteConnectionFactory db) => _db = db;

    public async Task<IEnumerable<MedicalHistoryEntry>> GetByPatientIdAsync(Guid patientId)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM MedicalHistoryEntries WHERE PatientId = @pid ORDER BY EventDate DESC";
        cmd.Parameters.AddWithValue("@pid", patientId.ToString());
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<MedicalHistoryEntry>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));
        return list;
    }

    public async Task<IEnumerable<MedicalHistoryEntry>> GetBySourceDocumentIdAsync(Guid sourceDocumentId)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM MedicalHistoryEntries WHERE SourceDocumentId = @sid ORDER BY EventDate";
        cmd.Parameters.AddWithValue("@sid", sourceDocumentId.ToString());
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<MedicalHistoryEntry>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));
        return list;
    }

    public async Task SaveRangeAsync(IEnumerable<MedicalHistoryEntry> entries)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        foreach (var e in entries)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = """
                INSERT OR REPLACE INTO MedicalHistoryEntries
                    (Id, PatientId, SourceDocumentId, Title, MedicationName, Dosage, Frequency,
                     Duration, Notes, Summary, Confidence, EventDate, CreatedAt, UpdatedAt, IsDirty, SyncedAt)
                VALUES
                    (@Id, @PatientId, @SourceDocumentId, @Title, @MedicationName, @Dosage, @Frequency,
                     @Duration, @Notes, @Summary, @Confidence, @EventDate, @CreatedAt, @UpdatedAt, @IsDirty, @SyncedAt)
                """;
            AddParams(cmd, e);
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    public async Task DeleteBySourceDocumentIdAsync(Guid sourceDocumentId)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM MedicalHistoryEntries WHERE SourceDocumentId = @sid";
        cmd.Parameters.AddWithValue("@sid", sourceDocumentId.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<MedicalHistoryEntry>> GetDirtyAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM MedicalHistoryEntries WHERE IsDirty = 1 ORDER BY EventDate";
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<MedicalHistoryEntry>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));
        return list;
    }

    public async Task MarkSyncedAsync(IEnumerable<Guid> ids)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var now = DateTime.UtcNow.ToString("O");
        foreach (var id in ids)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = "UPDATE MedicalHistoryEntries SET IsDirty = 0, SyncedAt = @now WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id.ToString());
            cmd.Parameters.AddWithValue("@now", now);
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    private static void AddParams(SqliteCommand cmd, MedicalHistoryEntry e)
    {
        cmd.Parameters.AddWithValue("@Id", e.Id.ToString());
        cmd.Parameters.AddWithValue("@PatientId", e.PatientId.ToString());
        cmd.Parameters.AddWithValue("@SourceDocumentId", e.SourceDocumentId.ToString());
        cmd.Parameters.AddWithValue("@Title", (object?)e.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MedicationName", (object?)e.MedicationName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Dosage", (object?)e.Dosage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Frequency", (object?)e.Frequency ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Duration", (object?)e.Duration ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Notes", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Summary", (object?)e.Summary ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Confidence", e.Confidence.ToString(System.Globalization.CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@EventDate", e.EventDate.ToString("O"));
        cmd.Parameters.AddWithValue("@CreatedAt", e.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@UpdatedAt", e.UpdatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@IsDirty", e.IsDirty ? 1L : 0L);
        cmd.Parameters.AddWithValue("@SyncedAt", e.SyncedAt.HasValue ? e.SyncedAt.Value.ToString("O") : DBNull.Value);
    }

    private static MedicalHistoryEntry ReadEntity(SqliteDataReader r) => new()
    {
        Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
        PatientId = Guid.Parse(r.GetString(r.GetOrdinal("PatientId"))),
        SourceDocumentId = Guid.Parse(r.GetString(r.GetOrdinal("SourceDocumentId"))),
        Title = r.IsDBNull(r.GetOrdinal("Title")) ? string.Empty : r.GetString(r.GetOrdinal("Title")),
        MedicationName = r.IsDBNull(r.GetOrdinal("MedicationName")) ? string.Empty : r.GetString(r.GetOrdinal("MedicationName")),
        Dosage = r.IsDBNull(r.GetOrdinal("Dosage")) ? string.Empty : r.GetString(r.GetOrdinal("Dosage")),
        Frequency = r.IsDBNull(r.GetOrdinal("Frequency")) ? string.Empty : r.GetString(r.GetOrdinal("Frequency")),
        Duration = r.IsDBNull(r.GetOrdinal("Duration")) ? string.Empty : r.GetString(r.GetOrdinal("Duration")),
        Notes = r.IsDBNull(r.GetOrdinal("Notes")) ? string.Empty : r.GetString(r.GetOrdinal("Notes")),
        Summary = r.IsDBNull(r.GetOrdinal("Summary")) ? string.Empty : r.GetString(r.GetOrdinal("Summary")),
        Confidence = decimal.Parse(r.GetString(r.GetOrdinal("Confidence")), System.Globalization.CultureInfo.InvariantCulture),
        EventDate = DateTime.Parse(r.GetString(r.GetOrdinal("EventDate"))),
        CreatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
        UpdatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("UpdatedAt"))),
        IsDirty = r.GetInt64(r.GetOrdinal("IsDirty")) != 0,
        SyncedAt = r.IsDBNull(r.GetOrdinal("SyncedAt")) ? null : DateTime.Parse(r.GetString(r.GetOrdinal("SyncedAt")))
    };
}
