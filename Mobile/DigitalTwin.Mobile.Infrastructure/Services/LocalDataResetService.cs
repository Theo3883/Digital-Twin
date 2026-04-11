using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace DigitalTwin.Mobile.Infrastructure.Services;

public sealed class LocalDataResetService : ILocalDataResetService
{
    private readonly SqliteConnectionFactory _db;

    public LocalDataResetService(SqliteConnectionFactory db) => _db = db;

    public async Task ResetAllAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        static async Task Exec(SqliteConnection conn, SqliteTransaction tx, string sql)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        // Order matters due to foreign keys (if enabled). We clear dependent tables first.
        await Exec(conn, (SqliteTransaction)tx, "DELETE FROM VitalSigns");
        await Exec(conn, (SqliteTransaction)tx, "DELETE FROM Medications");
        await Exec(conn, (SqliteTransaction)tx, "DELETE FROM SleepSessions");
        await Exec(conn, (SqliteTransaction)tx, "DELETE FROM EnvironmentReadings");
        await Exec(conn, (SqliteTransaction)tx, "DELETE FROM OcrDocuments");
        await Exec(conn, (SqliteTransaction)tx, "DELETE FROM MedicalHistoryEntries");
        await Exec(conn, (SqliteTransaction)tx, "DELETE FROM ChatMessages");
        await Exec(conn, (SqliteTransaction)tx, "DELETE FROM AppCache");
        await Exec(conn, (SqliteTransaction)tx, "DELETE FROM Patients");
        await Exec(conn, (SqliteTransaction)tx, "DELETE FROM Users");

        await tx.CommitAsync();
    }
}

