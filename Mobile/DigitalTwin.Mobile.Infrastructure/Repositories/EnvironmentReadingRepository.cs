using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace DigitalTwin.Mobile.Infrastructure.Repositories;

public class EnvironmentReadingRepository : IEnvironmentReadingRepository
{
    private readonly SqliteConnectionFactory _db;

    public EnvironmentReadingRepository(SqliteConnectionFactory db) => _db = db;

    public async Task SaveAsync(EnvironmentReading reading)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO EnvironmentReadings
                (Id, Latitude, Longitude, LocationDisplayName, PM25, PM10, O3, NO2,
                 Temperature, Humidity, AirQuality, AqiIndex, Timestamp, IsDirty, SyncedAt)
            VALUES
                (@Id, @Latitude, @Longitude, @LocationDisplayName, @PM25, @PM10, @O3, @NO2,
                 @Temperature, @Humidity, @AirQuality, @AqiIndex, @Timestamp, @IsDirty, @SyncedAt)
            """;
        AddParams(cmd, reading);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<EnvironmentReading?> GetLatestAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM EnvironmentReadings ORDER BY Timestamp DESC LIMIT 1";
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadEntity(r) : null;
    }

    public async Task<IEnumerable<EnvironmentReading>> GetSinceAsync(DateTime since, int limit = 200)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM EnvironmentReadings WHERE Timestamp >= @since ORDER BY Timestamp DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@since", since.ToString("O"));
        cmd.Parameters.AddWithValue("@limit", limit);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<EnvironmentReading>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));
        return list;
    }

    public async Task<IEnumerable<EnvironmentReading>> GetDirtyAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM EnvironmentReadings WHERE IsDirty = 1 ORDER BY Timestamp";
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<EnvironmentReading>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));
        return list;
    }

    public async Task MarkSyncedAsync(DateTime beforeOrAtTimestamp)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE EnvironmentReadings SET IsDirty = 0, SyncedAt = @now WHERE IsDirty = 1 AND Timestamp <= @before";
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@before", beforeOrAtTimestamp.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    private static void AddParams(SqliteCommand cmd, EnvironmentReading e)
    {
        cmd.Parameters.AddWithValue("@Id", e.Id.ToString());
        cmd.Parameters.AddWithValue("@Latitude", e.Latitude);
        cmd.Parameters.AddWithValue("@Longitude", e.Longitude);
        cmd.Parameters.AddWithValue("@LocationDisplayName", (object?)e.LocationDisplayName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PM25", e.PM25);
        cmd.Parameters.AddWithValue("@PM10", e.PM10);
        cmd.Parameters.AddWithValue("@O3", e.O3);
        cmd.Parameters.AddWithValue("@NO2", e.NO2);
        cmd.Parameters.AddWithValue("@Temperature", e.Temperature);
        cmd.Parameters.AddWithValue("@Humidity", e.Humidity);
        cmd.Parameters.AddWithValue("@AirQuality", (int)e.AirQuality);
        cmd.Parameters.AddWithValue("@AqiIndex", e.AqiIndex);
        cmd.Parameters.AddWithValue("@Timestamp", e.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@IsDirty", e.IsDirty ? 1L : 0L);
        cmd.Parameters.AddWithValue("@SyncedAt", e.SyncedAt.HasValue ? e.SyncedAt.Value.ToString("O") : DBNull.Value);
    }

    private static EnvironmentReading ReadEntity(SqliteDataReader r) => new()
    {
        Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
        Latitude = r.GetDouble(r.GetOrdinal("Latitude")),
        Longitude = r.GetDouble(r.GetOrdinal("Longitude")),
        LocationDisplayName = r.IsDBNull(r.GetOrdinal("LocationDisplayName")) ? string.Empty : r.GetString(r.GetOrdinal("LocationDisplayName")),
        PM25 = r.GetDouble(r.GetOrdinal("PM25")),
        PM10 = r.GetDouble(r.GetOrdinal("PM10")),
        O3 = r.GetDouble(r.GetOrdinal("O3")),
        NO2 = r.GetDouble(r.GetOrdinal("NO2")),
        Temperature = r.GetDouble(r.GetOrdinal("Temperature")),
        Humidity = r.GetDouble(r.GetOrdinal("Humidity")),
        AirQuality = (AirQualityLevel)r.GetInt32(r.GetOrdinal("AirQuality")),
        AqiIndex = r.GetInt32(r.GetOrdinal("AqiIndex")),
        Timestamp = DateTime.Parse(r.GetString(r.GetOrdinal("Timestamp"))),
        IsDirty = r.GetInt64(r.GetOrdinal("IsDirty")) != 0,
        SyncedAt = r.IsDBNull(r.GetOrdinal("SyncedAt")) ? null : DateTime.Parse(r.GetString(r.GetOrdinal("SyncedAt")))
    };
}
