using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Infrastructure.Repositories;

public class SleepSessionRepository : ISleepSessionRepository
{
    private readonly SqliteConnectionFactory _db;
    private readonly ILogger<SleepSessionRepository> _logger;

    public SleepSessionRepository(SqliteConnectionFactory db, ILogger<SleepSessionRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IEnumerable<SleepSession>> GetByPatientIdAsync(Guid patientId, DateTime? from = null, DateTime? to = null)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var effectiveFrom = from ?? DateTime.MinValue;
        var effectiveTo = to ?? DateTime.MaxValue;
        var totalRows = await CountSleepRowsAsync(conn);
        var patientRows = await CountSleepRowsForPatientAsync(conn, patientId);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM SleepSessions WHERE PatientId = @pid AND StartTime >= @from AND StartTime <= @to ORDER BY StartTime DESC";
        cmd.Parameters.AddWithValue("@pid", patientId.ToString());
        cmd.Parameters.AddWithValue("@from", effectiveFrom.ToString("O"));
        cmd.Parameters.AddWithValue("@to", effectiveTo.ToString("O"));
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<SleepSession>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));

        if (list.Count > 0)
        {
            var latest = list[0];
            _logger.LogInformation("[SleepDebug][SleepRepo] Query result count={Count} patientId={PatientId} from={From} to={To} totalRows={TotalRows} patientRows={PatientRows} latestStart={LatestStart} latestEnd={LatestEnd}",
                list.Count,
                patientId,
                effectiveFrom.ToString("O"),
                effectiveTo.ToString("O"),
                totalRows,
                patientRows,
                latest.StartTime.ToString("O"),
                latest.EndTime.ToString("O"));
        }
        else
        {
            _logger.LogWarning("[SleepDebug][SleepRepo] Query returned 0 rows. patientId={PatientId} from={From} to={To} totalRows={TotalRows} patientRows={PatientRows}",
                patientId,
                effectiveFrom.ToString("O"),
                effectiveTo.ToString("O"),
                totalRows,
                patientRows);
        }

        return list;
    }

    public async Task SaveAsync(SleepSession session)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO SleepSessions
                (Id, PatientId, StartTime, EndTime, DurationMinutes, QualityScore, CreatedAt, IsSynced)
            VALUES
                (@Id, @PatientId, @StartTime, @EndTime, @DurationMinutes, @QualityScore, @CreatedAt, @IsSynced)
            """;
        AddParams(cmd, session);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveRangeAsync(IEnumerable<SleepSession> sessions)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        foreach (var s in sessions)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = """
                INSERT OR REPLACE INTO SleepSessions
                    (Id, PatientId, StartTime, EndTime, DurationMinutes, QualityScore, CreatedAt, IsSynced)
                VALUES
                    (@Id, @PatientId, @StartTime, @EndTime, @DurationMinutes, @QualityScore, @CreatedAt, @IsSynced)
                """;
            AddParams(cmd, s);
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    public async Task<bool> ExistsAsync(Guid patientId, DateTime startTime)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM SleepSessions WHERE PatientId = @pid AND StartTime = @st";
        cmd.Parameters.AddWithValue("@pid", patientId.ToString());
        cmd.Parameters.AddWithValue("@st", startTime.ToString("O"));
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result) > 0;
    }

    public async Task<IEnumerable<SleepSession>> GetUnsyncedAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM SleepSessions WHERE IsSynced = 0 ORDER BY StartTime";
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<SleepSession>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));
        return list;
    }

    public async Task MarkAsSyncedAsync(Guid patientId, DateTime beforeTimestamp)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE SleepSessions SET IsSynced = 1 WHERE PatientId = @pid AND IsSynced = 0 AND StartTime <= @before";
        cmd.Parameters.AddWithValue("@pid", patientId.ToString());
        cmd.Parameters.AddWithValue("@before", beforeTimestamp.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    private static void AddParams(SqliteCommand cmd, SleepSession s)
    {
        cmd.Parameters.AddWithValue("@Id", s.Id.ToString());
        cmd.Parameters.AddWithValue("@PatientId", s.PatientId.ToString());
        cmd.Parameters.AddWithValue("@StartTime", s.StartTime.ToString("O"));
        cmd.Parameters.AddWithValue("@EndTime", s.EndTime.ToString("O"));
        cmd.Parameters.AddWithValue("@DurationMinutes", s.DurationMinutes);
        cmd.Parameters.AddWithValue("@QualityScore", s.QualityScore);
        cmd.Parameters.AddWithValue("@CreatedAt", s.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@IsSynced", s.IsSynced ? 1L : 0L);
    }

    private static SleepSession ReadEntity(SqliteDataReader r) => new()
    {
        Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
        PatientId = Guid.Parse(r.GetString(r.GetOrdinal("PatientId"))),
        StartTime = DateTime.Parse(r.GetString(r.GetOrdinal("StartTime"))),
        EndTime = DateTime.Parse(r.GetString(r.GetOrdinal("EndTime"))),
        DurationMinutes = r.GetInt32(r.GetOrdinal("DurationMinutes")),
        QualityScore = r.GetDouble(r.GetOrdinal("QualityScore")),
        CreatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
        IsSynced = r.GetInt64(r.GetOrdinal("IsSynced")) != 0
    };

    private static async Task<long> CountSleepRowsAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM SleepSessions";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    private static async Task<long> CountSleepRowsForPatientAsync(SqliteConnection conn, Guid patientId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM SleepSessions WHERE PatientId = @pid";
        cmd.Parameters.AddWithValue("@pid", patientId.ToString());
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }
}
