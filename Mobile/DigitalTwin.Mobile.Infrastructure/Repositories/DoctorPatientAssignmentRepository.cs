using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace DigitalTwin.Mobile.Infrastructure.Repositories;

public sealed class DoctorPatientAssignmentRepository : IDoctorPatientAssignmentRepository
{
    private readonly SqliteConnectionFactory _db;

    public DoctorPatientAssignmentRepository(SqliteConnectionFactory db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AssignedDoctor>> GetByUserIdAsync(Guid userId)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DoctorId, FullName, Email, PhotoUrl, AssignedAt, Notes
            FROM DoctorPatientAssignments
            WHERE UserId = @userId
            ORDER BY AssignedAt DESC
            """;
        cmd.Parameters.AddWithValue("@userId", userId.ToString());

        await using var reader = await cmd.ExecuteReaderAsync();
        var results = new List<AssignedDoctor>();

        while (await reader.ReadAsync())
        {
            var assignedAtRaw = reader.GetString(reader.GetOrdinal("AssignedAt"));
            var assignedAt = DateTime.TryParse(assignedAtRaw, out var parsedAssignedAt)
                ? parsedAssignedAt
                : DateTime.UnixEpoch;

            results.Add(new AssignedDoctor
            {
                DoctorId = Guid.Parse(reader.GetString(reader.GetOrdinal("DoctorId"))),
                FullName = reader.IsDBNull(reader.GetOrdinal("FullName")) ? string.Empty : reader.GetString(reader.GetOrdinal("FullName")),
                Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? string.Empty : reader.GetString(reader.GetOrdinal("Email")),
                PhotoUrl = reader.IsDBNull(reader.GetOrdinal("PhotoUrl")) ? null : reader.GetString(reader.GetOrdinal("PhotoUrl")),
                AssignedAt = assignedAt,
                Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes"))
            });
        }

        return results;
    }

    public async Task ReplaceForUserAsync(Guid userId, IEnumerable<AssignedDoctor> doctors)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using (var deleteCmd = conn.CreateCommand())
        {
            deleteCmd.Transaction = (SqliteTransaction)tx;
            deleteCmd.CommandText = "DELETE FROM DoctorPatientAssignments WHERE UserId = @userId";
            deleteCmd.Parameters.AddWithValue("@userId", userId.ToString());
            await deleteCmd.ExecuteNonQueryAsync();
        }

        foreach (var doctor in doctors
                     .GroupBy(d => d.DoctorId)
                     .Select(group => group.First()))
        {
            await using var insertCmd = conn.CreateCommand();
            insertCmd.Transaction = (SqliteTransaction)tx;
            insertCmd.CommandText = """
                INSERT INTO DoctorPatientAssignments
                    (UserId, DoctorId, FullName, Email, PhotoUrl, AssignedAt, Notes)
                VALUES
                    (@UserId, @DoctorId, @FullName, @Email, @PhotoUrl, @AssignedAt, @Notes)
                """;
            insertCmd.Parameters.AddWithValue("@UserId", userId.ToString());
            insertCmd.Parameters.AddWithValue("@DoctorId", doctor.DoctorId.ToString());
            insertCmd.Parameters.AddWithValue("@FullName", (object?)doctor.FullName ?? string.Empty);
            insertCmd.Parameters.AddWithValue("@Email", (object?)doctor.Email ?? string.Empty);
            insertCmd.Parameters.AddWithValue("@PhotoUrl", (object?)doctor.PhotoUrl ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@AssignedAt", doctor.AssignedAt.ToString("O"));
            insertCmd.Parameters.AddWithValue("@Notes", (object?)doctor.Notes ?? DBNull.Value);

            await insertCmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }
}
