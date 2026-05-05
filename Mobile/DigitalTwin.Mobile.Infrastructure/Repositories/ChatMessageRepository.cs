using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace DigitalTwin.Mobile.Infrastructure.Repositories;

public class ChatMessageRepository : IChatMessageRepository
{
    private readonly SqliteConnectionFactory _db;

    public ChatMessageRepository(SqliteConnectionFactory db) => _db = db;

    public async Task<IEnumerable<ChatMessage>> GetAllAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ChatMessages ORDER BY Timestamp";
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<ChatMessage>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));
        return list;
    }

    public async Task SaveAsync(ChatMessage message)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO ChatMessages (Id, Content, IsUser, Timestamp)
            VALUES (@Id, @Content, @IsUser, @Timestamp)
            """;
        cmd.Parameters.AddWithValue("@Id", message.Id.ToString());
        cmd.Parameters.AddWithValue("@Content", message.Content);
        cmd.Parameters.AddWithValue("@IsUser", message.IsUser ? 1L : 0L);
        cmd.Parameters.AddWithValue("@Timestamp", message.Timestamp.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ClearAllAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ChatMessages";
        await cmd.ExecuteNonQueryAsync();
    }

    private static ChatMessage ReadEntity(SqliteDataReader r) => new()
    {
        Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
        Content = r.GetString(r.GetOrdinal("Content")),
        IsUser = r.GetInt64(r.GetOrdinal("IsUser")) != 0,
        Timestamp = DateTime.Parse(r.GetString(r.GetOrdinal("Timestamp")))
    };
}
