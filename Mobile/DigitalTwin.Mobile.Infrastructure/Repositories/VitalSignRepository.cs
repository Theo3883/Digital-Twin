using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace DigitalTwin.Mobile.Infrastructure.Repositories;

public class VitalSignRepository : IVitalSignRepository
{
    private readonly SqliteConnectionFactory _db;

    public VitalSignRepository(SqliteConnectionFactory db) => _db = db;

    public async Task<VitalSign?> GetByIdAsync(Guid id)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM VitalSigns WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadEntity(r) : null;
    }

    public async Task<IEnumerable<VitalSign>> GetByPatientIdAsync(Guid patientId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM VitalSigns WHERE PatientId = @pid AND Timestamp >= @from AND Timestamp <= @to ORDER BY Timestamp DESC";
        cmd.Parameters.AddWithValue("@pid", patientId.ToString());
        cmd.Parameters.AddWithValue("@from", (fromDate ?? DateTime.MinValue).ToString("O"));
        cmd.Parameters.AddWithValue("@to", (toDate ?? DateTime.MaxValue).ToString("O"));
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<VitalSign>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));
        return list;
    }

    public async Task<IEnumerable<VitalSign>> GetByTypeAsync(Guid patientId, VitalSignType type, DateTime? fromDate = null, DateTime? toDate = null)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM VitalSigns WHERE PatientId = @pid AND Type = @type AND Timestamp >= @from AND Timestamp <= @to ORDER BY Timestamp DESC";
        cmd.Parameters.AddWithValue("@pid", patientId.ToString());
        cmd.Parameters.AddWithValue("@type", (int)type);
        cmd.Parameters.AddWithValue("@from", (fromDate ?? DateTime.MinValue).ToString("O"));
        cmd.Parameters.AddWithValue("@to", (toDate ?? DateTime.MaxValue).ToString("O"));
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<VitalSign>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));
        return list;
    }

    public async Task<bool> ExistsAsync(Guid patientId, VitalSignType type, DateTime timestamp, string source)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT 1
            FROM VitalSigns
            WHERE PatientId = @pid
              AND Type = @type
              AND Timestamp = @ts
              AND Source = @source
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@pid", patientId.ToString());
        cmd.Parameters.AddWithValue("@type", (int)type);
        cmd.Parameters.AddWithValue("@ts", timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@source", source);

        var result = await cmd.ExecuteScalarAsync();
        return result != null && result != DBNull.Value;
    }

    public async Task<Guid?> GetIdByKeyAsync(Guid patientId, VitalSignType type, DateTime timestamp, string source)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id
            FROM VitalSigns
            WHERE PatientId = @pid
              AND Type = @type
              AND Timestamp = @ts
              AND Source = @source
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@pid", patientId.ToString());
        cmd.Parameters.AddWithValue("@type", (int)type);
        cmd.Parameters.AddWithValue("@ts", timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@source", source);

        var result = await cmd.ExecuteScalarAsync();
        if (result == null || result == DBNull.Value) return null;
        return Guid.TryParse(result.ToString(), out var id) ? id : null;
    }

    public async Task SaveAsync(VitalSign vitalSign)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO VitalSigns
                (Id, PatientId, Type, Value, Unit, Source, Timestamp, CreatedAt, IsSynced)
            VALUES
                (@Id, @PatientId, @Type, @Value, @Unit, @Source, @Timestamp, @CreatedAt, @IsSynced)
            """;
        AddParams(cmd, vitalSign);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveRangeAsync(IEnumerable<VitalSign> vitalSigns)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        foreach (var vs in vitalSigns)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = """
                INSERT OR REPLACE INTO VitalSigns
                    (Id, PatientId, Type, Value, Unit, Source, Timestamp, CreatedAt, IsSynced)
                VALUES
                    (@Id, @PatientId, @Type, @Value, @Unit, @Source, @Timestamp, @CreatedAt, @IsSynced)
                """;
            AddParams(cmd, vs);
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    public async Task<IEnumerable<VitalSign>> GetUnsyncedAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM VitalSigns WHERE IsSynced = 0 ORDER BY Timestamp";
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<VitalSign>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));
        return list;
    }

    public async Task MarkAsSyncedAsync(IEnumerable<Guid> ids)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        foreach (var id in ids)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = "UPDATE VitalSigns SET IsSynced = 1 WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id.ToString());
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    private static void AddParams(SqliteCommand cmd, VitalSign v)
    {
        cmd.Parameters.AddWithValue("@Id", v.Id.ToString());
        cmd.Parameters.AddWithValue("@PatientId", v.PatientId.ToString());
        cmd.Parameters.AddWithValue("@Type", (int)v.Type);
        cmd.Parameters.AddWithValue("@Value", v.Value);
        cmd.Parameters.AddWithValue("@Unit", v.Unit);
        cmd.Parameters.AddWithValue("@Source", v.Source);
        cmd.Parameters.AddWithValue("@Timestamp", v.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@CreatedAt", v.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@IsSynced", v.IsSynced ? 1L : 0L);
    }

    private static VitalSign ReadEntity(SqliteDataReader r) => new()
    {
        Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
        PatientId = Guid.Parse(r.GetString(r.GetOrdinal("PatientId"))),
        Type = (VitalSignType)r.GetInt32(r.GetOrdinal("Type")),
        Value = r.GetDouble(r.GetOrdinal("Value")),
        Unit = r.GetString(r.GetOrdinal("Unit")),
        Source = r.GetString(r.GetOrdinal("Source")),
        Timestamp = DateTime.Parse(r.GetString(r.GetOrdinal("Timestamp"))),
        CreatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
        IsSynced = r.GetInt64(r.GetOrdinal("IsSynced")) != 0
    };
}