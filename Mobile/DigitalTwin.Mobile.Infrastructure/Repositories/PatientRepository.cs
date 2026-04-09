using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace DigitalTwin.Mobile.Infrastructure.Repositories;

public class PatientRepository : IPatientRepository
{
    private readonly SqliteConnectionFactory _db;

    public PatientRepository(SqliteConnectionFactory db) => _db = db;

    public async Task<Patient?> GetByIdAsync(Guid id)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Patients WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadEntity(r) : null;
    }

    public async Task<Patient?> GetByUserIdAsync(Guid userId)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Patients WHERE UserId = @userId LIMIT 1";
        cmd.Parameters.AddWithValue("@userId", userId.ToString());
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadEntity(r) : null;
    }

    public async Task<Patient?> GetCurrentPatientAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        // Get first user
        await using var userCmd = conn.CreateCommand();
        userCmd.CommandText = "SELECT Id FROM Users ORDER BY CreatedAt LIMIT 1";
        var userIdObj = await userCmd.ExecuteScalarAsync();
        if (userIdObj is not string userIdStr) return null;

        // Get patient by that user's ID
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Patients WHERE UserId = @userId LIMIT 1";
        cmd.Parameters.AddWithValue("@userId", userIdStr);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadEntity(r) : null;
    }

    public async Task SaveAsync(Patient patient)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO Patients
                (Id, UserId, BloodType, Allergies, MedicalHistoryNotes, Weight, Height,
                 BloodPressureSystolic, BloodPressureDiastolic, Cholesterol, Cnp, CreatedAt, UpdatedAt, IsSynced)
            VALUES
                (@Id, @UserId, @BloodType, @Allergies, @MedicalHistoryNotes, @Weight, @Height,
                 @BloodPressureSystolic, @BloodPressureDiastolic, @Cholesterol, @Cnp, @CreatedAt, @UpdatedAt, @IsSynced)
            """;
        cmd.Parameters.AddWithValue("@Id", patient.Id.ToString());
        cmd.Parameters.AddWithValue("@UserId", patient.UserId.ToString());
        cmd.Parameters.AddWithValue("@BloodType", (object?)patient.BloodType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Allergies", (object?)patient.Allergies ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MedicalHistoryNotes", (object?)patient.MedicalHistoryNotes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Weight", patient.Weight.HasValue ? patient.Weight.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@Height", patient.Height.HasValue ? patient.Height.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@BloodPressureSystolic", patient.BloodPressureSystolic.HasValue ? patient.BloodPressureSystolic.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@BloodPressureDiastolic", patient.BloodPressureDiastolic.HasValue ? patient.BloodPressureDiastolic.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@Cholesterol", patient.Cholesterol.HasValue ? patient.Cholesterol.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@Cnp", (object?)patient.Cnp ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", patient.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@UpdatedAt", patient.UpdatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@IsSynced", patient.IsSynced ? 1L : 0L);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<Patient>> GetUnsyncedAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Patients WHERE IsSynced = 0";
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<Patient>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));
        return list;
    }

    public async Task MarkAsSyncedAsync(Guid id)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Patients SET IsSynced = 1 WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    private static Patient ReadEntity(SqliteDataReader r) => new()
    {
        Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
        UserId = Guid.Parse(r.GetString(r.GetOrdinal("UserId"))),
        BloodType = r.IsDBNull(r.GetOrdinal("BloodType")) ? null : r.GetString(r.GetOrdinal("BloodType")),
        Allergies = r.IsDBNull(r.GetOrdinal("Allergies")) ? null : r.GetString(r.GetOrdinal("Allergies")),
        MedicalHistoryNotes = r.IsDBNull(r.GetOrdinal("MedicalHistoryNotes")) ? null : r.GetString(r.GetOrdinal("MedicalHistoryNotes")),
        Weight = r.IsDBNull(r.GetOrdinal("Weight")) ? null : r.GetDouble(r.GetOrdinal("Weight")),
        Height = r.IsDBNull(r.GetOrdinal("Height")) ? null : r.GetDouble(r.GetOrdinal("Height")),
        BloodPressureSystolic = r.IsDBNull(r.GetOrdinal("BloodPressureSystolic")) ? null : r.GetInt32(r.GetOrdinal("BloodPressureSystolic")),
        BloodPressureDiastolic = r.IsDBNull(r.GetOrdinal("BloodPressureDiastolic")) ? null : r.GetInt32(r.GetOrdinal("BloodPressureDiastolic")),
        Cholesterol = r.IsDBNull(r.GetOrdinal("Cholesterol")) ? null : r.GetDouble(r.GetOrdinal("Cholesterol")),
        Cnp = r.IsDBNull(r.GetOrdinal("Cnp")) ? null : r.GetString(r.GetOrdinal("Cnp")),
        CreatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
        UpdatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("UpdatedAt"))),
        IsSynced = r.GetInt64(r.GetOrdinal("IsSynced")) != 0
    };
}