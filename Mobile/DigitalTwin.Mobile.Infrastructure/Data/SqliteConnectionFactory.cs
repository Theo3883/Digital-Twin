using Microsoft.Data.Sqlite;

namespace DigitalTwin.Mobile.Infrastructure.Data;

/// <summary>
/// NativeAOT-safe SQLite connection factory.
/// Bypasses EF Core's model building pipeline entirely.
/// </summary>
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SqliteConnection CreateConnection() => new(_connectionString);
}
