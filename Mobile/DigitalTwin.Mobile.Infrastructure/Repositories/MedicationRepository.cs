using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace DigitalTwin.Mobile.Infrastructure.Repositories;

public class MedicationRepository : IMedicationRepository
{
    private readonly SqliteConnectionFactory _db;

    public MedicationRepository(SqliteConnectionFactory db) => _db = db;

    public async Task<IEnumerable<Medication>> GetByPatientIdAsync(Guid patientId)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Medications WHERE PatientId = @pid ORDER BY CreatedAt DESC";
        cmd.Parameters.AddWithValue("@pid", patientId.ToString());
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<Medication>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));
        return list;
    }

    public async Task<IEnumerable<Medication>> GetActiveByPatientIdAsync(Guid patientId)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Medications WHERE PatientId = @pid AND Status = @status ORDER BY CreatedAt DESC";
        cmd.Parameters.AddWithValue("@pid", patientId.ToString());
        cmd.Parameters.AddWithValue("@status", (int)MedicationStatus.Active);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<Medication>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));
        return list;
    }

    public async Task<Medication?> GetByIdAsync(Guid id)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Medications WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadEntity(r) : null;
    }

    public async Task SaveAsync(Medication medication)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO Medications
                (Id, PatientId, Name, Dosage, Frequency, Route, RxCui, Instructions, Reason,
                 PrescribedByUserId, StartDate, EndDate, Status, DiscontinuedReason, AddedByRole,
                 CreatedAt, UpdatedAt, IsSynced)
            VALUES
                (@Id, @PatientId, @Name, @Dosage, @Frequency, @Route, @RxCui, @Instructions, @Reason,
                 @PrescribedByUserId, @StartDate, @EndDate, @Status, @DiscontinuedReason, @AddedByRole,
                 @CreatedAt, @UpdatedAt, @IsSynced)
            """;
        AddParams(cmd, medication);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(Medication medication)
    {
        await SaveAsync(medication);
    }

    public async Task<IEnumerable<Medication>> GetUnsyncedAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Medications WHERE IsSynced = 0";
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<Medication>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));
        return list;
    }

    public async Task MarkAsSyncedAsync(Guid patientId)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Medications SET IsSynced = 1 WHERE PatientId = @pid AND IsSynced = 0";
        cmd.Parameters.AddWithValue("@pid", patientId.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    private static void AddParams(SqliteCommand cmd, Medication m)
    {
        cmd.Parameters.AddWithValue("@Id", m.Id.ToString());
        cmd.Parameters.AddWithValue("@PatientId", m.PatientId.ToString());
        cmd.Parameters.AddWithValue("@Name", m.Name);
        cmd.Parameters.AddWithValue("@Dosage", m.Dosage);
        cmd.Parameters.AddWithValue("@Frequency", (object?)m.Frequency ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Route", (int)m.Route);
        cmd.Parameters.AddWithValue("@RxCui", (object?)m.RxCui ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Instructions", (object?)m.Instructions ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Reason", (object?)m.Reason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PrescribedByUserId", m.PrescribedByUserId.HasValue ? m.PrescribedByUserId.Value.ToString() : DBNull.Value);
        cmd.Parameters.AddWithValue("@StartDate", m.StartDate.HasValue ? m.StartDate.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@EndDate", m.EndDate.HasValue ? m.EndDate.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", (int)m.Status);
        cmd.Parameters.AddWithValue("@DiscontinuedReason", (object?)m.DiscontinuedReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AddedByRole", (int)m.AddedByRole);
        cmd.Parameters.AddWithValue("@CreatedAt", m.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@UpdatedAt", m.UpdatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@IsSynced", m.IsSynced ? 1L : 0L);
    }

    private static Medication ReadEntity(SqliteDataReader r) => new()
    {
        Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
        PatientId = Guid.Parse(r.GetString(r.GetOrdinal("PatientId"))),
        Name = r.GetString(r.GetOrdinal("Name")),
        Dosage = r.GetString(r.GetOrdinal("Dosage")),
        Frequency = r.IsDBNull(r.GetOrdinal("Frequency")) ? null : r.GetString(r.GetOrdinal("Frequency")),
        Route = (MedicationRoute)r.GetInt32(r.GetOrdinal("Route")),
        RxCui = r.IsDBNull(r.GetOrdinal("RxCui")) ? null : r.GetString(r.GetOrdinal("RxCui")),
        Instructions = r.IsDBNull(r.GetOrdinal("Instructions")) ? null : r.GetString(r.GetOrdinal("Instructions")),
        Reason = r.IsDBNull(r.GetOrdinal("Reason")) ? null : r.GetString(r.GetOrdinal("Reason")),
        PrescribedByUserId = r.IsDBNull(r.GetOrdinal("PrescribedByUserId")) ? null : Guid.Parse(r.GetString(r.GetOrdinal("PrescribedByUserId"))),
        StartDate = r.IsDBNull(r.GetOrdinal("StartDate")) ? null : DateTime.Parse(r.GetString(r.GetOrdinal("StartDate"))),
        EndDate = r.IsDBNull(r.GetOrdinal("EndDate")) ? null : DateTime.Parse(r.GetString(r.GetOrdinal("EndDate"))),
        Status = (MedicationStatus)r.GetInt32(r.GetOrdinal("Status")),
        DiscontinuedReason = r.IsDBNull(r.GetOrdinal("DiscontinuedReason")) ? null : r.GetString(r.GetOrdinal("DiscontinuedReason")),
        AddedByRole = (AddedByRole)r.GetInt32(r.GetOrdinal("AddedByRole")),
        CreatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
        UpdatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("UpdatedAt"))),
        IsSynced = r.GetInt64(r.GetOrdinal("IsSynced")) != 0
    };
}
