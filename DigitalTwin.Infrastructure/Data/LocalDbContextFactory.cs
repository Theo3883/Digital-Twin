using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DigitalTwin.Infrastructure.Data;

public sealed class LocalDbContextFactory : IDesignTimeDbContextFactory<LocalDbContext>
{
    public LocalDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SQLITE_CONNECTION_STRING")
            ?? "Data Source=healthapp.db";

        var optionsBuilder = new DbContextOptionsBuilder<LocalDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        return new LocalDbContext(optionsBuilder.Options);
    }
}
