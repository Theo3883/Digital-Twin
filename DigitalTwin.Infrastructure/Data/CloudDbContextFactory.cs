using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DigitalTwin.Infrastructure.Data;

public sealed class CloudDbContextFactory : IDesignTimeDbContextFactory<CloudDbContext>
{
    public CloudDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=healthapp;Username=healthapp;Password=healthapp_dev";

        var optionsBuilder = new DbContextOptionsBuilder<CloudDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new CloudDbContext(optionsBuilder.Options);
    }
}
