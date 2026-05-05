using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Infrastructure.Data;

namespace DigitalTwin.Mobile.Infrastructure.Services;

/// <summary>
/// Persist the cloud access token in local SQLite so the NativeAOT engine
/// can restore authenticated cloud sync after app restarts.
/// </summary>
public sealed class SqliteAccessTokenStore : IAccessTokenStore
{
    private const string Key = "cloud.accessToken";
    private readonly SqliteConnectionFactory _db;

    public SqliteAccessTokenStore(SqliteConnectionFactory db)
    {
        _db = db;
    }

    public string? AccessToken
    {
        get
        {
            try
            {
                using var conn = _db.CreateConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT ValueJson FROM AppCache WHERE Key = @key LIMIT 1";
                cmd.Parameters.AddWithValue("@key", Key);
                var value = cmd.ExecuteScalar() as string;
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
            catch
            {
                return null;
            }
        }
        set
        {
            try
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    Clear();
                    return;
                }

                var now = DateTime.UtcNow.ToString("O");
                using var conn = _db.CreateConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO AppCache (Key, ValueJson, StoredAtUtc, Fingerprint, UpdatedAtUtc)
                    VALUES (@key, @valueJson, @storedAtUtc, NULL, @updatedAtUtc)
                    ON CONFLICT(Key) DO UPDATE SET
                        ValueJson = excluded.ValueJson,
                        StoredAtUtc = excluded.StoredAtUtc,
                        UpdatedAtUtc = excluded.UpdatedAtUtc
                    """;
                cmd.Parameters.AddWithValue("@key", Key);
                cmd.Parameters.AddWithValue("@valueJson", value.Trim());
                cmd.Parameters.AddWithValue("@storedAtUtc", now);
                cmd.Parameters.AddWithValue("@updatedAtUtc", now);
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // ignore persistence failures; cloud will behave as unauthenticated
            }
        }
    }

    public void Clear()
    {
        try
        {
            using var conn = _db.CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM AppCache WHERE Key = @key";
            cmd.Parameters.AddWithValue("@key", Key);
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // ignore
        }
    }
}

