using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace DigitalTwin.Mobile.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly SqliteConnectionFactory _db;

    public UserRepository(SqliteConnectionFactory db) => _db = db;

    public async Task<User?> GetByIdAsync(Guid id)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadEntity(r) : null;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users WHERE Email = @email LIMIT 1";
        cmd.Parameters.AddWithValue("@email", email);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadEntity(r) : null;
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users ORDER BY CreatedAt LIMIT 1";
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadEntity(r) : null;
    }

    public async Task SaveAsync(User user)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO Users
                (Id, Email, Role, FirstName, LastName, PhotoUrl, Phone, Address, City, Country, DateOfBirth, CreatedAt, UpdatedAt, IsSynced)
            VALUES
                (@Id, @Email, @Role, @FirstName, @LastName, @PhotoUrl, @Phone, @Address, @City, @Country, @DateOfBirth, @CreatedAt, @UpdatedAt, @IsSynced)
            """;
        cmd.Parameters.AddWithValue("@Id", user.Id.ToString());
        cmd.Parameters.AddWithValue("@Email", user.Email);
        cmd.Parameters.AddWithValue("@Role", (int)user.Role);
        cmd.Parameters.AddWithValue("@FirstName", (object?)user.FirstName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LastName", (object?)user.LastName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PhotoUrl", (object?)user.PhotoUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Phone", (object?)user.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Address", (object?)user.Address ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@City", (object?)user.City ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Country", (object?)user.Country ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DateOfBirth", user.DateOfBirth.HasValue ? user.DateOfBirth.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", user.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@UpdatedAt", user.UpdatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@IsSynced", user.IsSynced ? 1L : 0L);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<User>> GetUnsyncedAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users WHERE IsSynced = 0";
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<User>();
        while (await r.ReadAsync()) list.Add(ReadEntity(r));
        return list;
    }

    public async Task MarkAsSyncedAsync(Guid id)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Users SET IsSynced = 1 WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    private static User ReadEntity(SqliteDataReader r) => new()
    {
        Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
        Email = r.GetString(r.GetOrdinal("Email")),
        Role = (UserRole)r.GetInt32(r.GetOrdinal("Role")),
        FirstName = r.IsDBNull(r.GetOrdinal("FirstName")) ? null : r.GetString(r.GetOrdinal("FirstName")),
        LastName = r.IsDBNull(r.GetOrdinal("LastName")) ? null : r.GetString(r.GetOrdinal("LastName")),
        PhotoUrl = r.IsDBNull(r.GetOrdinal("PhotoUrl")) ? null : r.GetString(r.GetOrdinal("PhotoUrl")),
        Phone = r.IsDBNull(r.GetOrdinal("Phone")) ? null : r.GetString(r.GetOrdinal("Phone")),
        Address = ReadOptionalString(r, "Address"),
        City = ReadOptionalString(r, "City"),
        Country = ReadOptionalString(r, "Country"),
        DateOfBirth = r.IsDBNull(r.GetOrdinal("DateOfBirth")) ? null : DateTime.Parse(r.GetString(r.GetOrdinal("DateOfBirth"))),
        CreatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
        UpdatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("UpdatedAt"))),
        IsSynced = r.GetInt64(r.GetOrdinal("IsSynced")) != 0
    };

    private static string? ReadOptionalString(SqliteDataReader r, string column)
    {
        try
        {
            var ordinal = r.GetOrdinal(column);
            return r.IsDBNull(ordinal) ? null : r.GetString(ordinal);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Column doesn't exist yet (pre-migration DB). Return null.
            return null;
        }
    }
}