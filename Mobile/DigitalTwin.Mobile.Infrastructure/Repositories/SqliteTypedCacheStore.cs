using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Infrastructure.Repositories;

public sealed class SqliteTypedCacheStore : ITypedCacheStore
{
    private readonly SqliteConnectionFactory _db;
    private readonly ILogger<SqliteTypedCacheStore> _logger;

    public SqliteTypedCacheStore(SqliteConnectionFactory db, ILogger<SqliteTypedCacheStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CacheEnvelope<T>?> GetAsync<T>(string key, JsonTypeInfo<T> typeInfo, CancellationToken ct = default)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ValueJson, StoredAtUtc, Fingerprint FROM AppCache WHERE Key = @key LIMIT 1";
        cmd.Parameters.AddWithValue("@key", key);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var valueJson = reader.GetString(0);
        var storedAtRaw = reader.GetString(1);
        var fingerprint = reader.IsDBNull(2) ? null : reader.GetString(2);

        try
        {
            var value = JsonSerializer.Deserialize(valueJson, typeInfo);
            if (value == null)
                return null;

            if (!DateTime.TryParse(storedAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var storedAt))
                storedAt = DateTime.UtcNow;

            return new CacheEnvelope<T>
            {
                Value = value,
                StoredAtUtc = storedAt.Kind == DateTimeKind.Utc ? storedAt : storedAt.ToUniversalTime(),
                Fingerprint = fingerprint
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TypedCache] Failed to deserialize cache key {Key}. Removing corrupted entry.", key);
            await RemoveAsync(key, ct);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, CacheEnvelope<T> envelope, JsonTypeInfo<T> typeInfo, CancellationToken ct = default)
    {
        var utcStoredAt = envelope.StoredAtUtc.Kind == DateTimeKind.Utc
            ? envelope.StoredAtUtc
            : envelope.StoredAtUtc.ToUniversalTime();
        var now = DateTime.UtcNow;
        var valueJson = JsonSerializer.Serialize(envelope.Value, typeInfo);

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO AppCache (Key, ValueJson, StoredAtUtc, Fingerprint, UpdatedAtUtc)
            VALUES (@key, @valueJson, @storedAtUtc, @fingerprint, @updatedAtUtc)
            ON CONFLICT(Key) DO UPDATE SET
                ValueJson = excluded.ValueJson,
                StoredAtUtc = excluded.StoredAtUtc,
                Fingerprint = excluded.Fingerprint,
                UpdatedAtUtc = excluded.UpdatedAtUtc
            """;
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@valueJson", valueJson);
        cmd.Parameters.AddWithValue("@storedAtUtc", utcStoredAt.ToString("O"));
        cmd.Parameters.AddWithValue("@fingerprint", (object?)envelope.Fingerprint ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@updatedAtUtc", now.ToString("O"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM AppCache WHERE Key = @key";
        cmd.Parameters.AddWithValue("@key", key);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM AppCache WHERE Key LIKE @prefix";
        cmd.Parameters.AddWithValue("@prefix", string.Concat(prefix, "%"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default)
    {
        var cutoff = cutoffUtc.Kind == DateTimeKind.Utc ? cutoffUtc : cutoffUtc.ToUniversalTime();

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM AppCache WHERE StoredAtUtc < @cutoffUtc";
        cmd.Parameters.AddWithValue("@cutoffUtc", cutoff.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
