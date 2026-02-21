using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DigitalTwin.Infrastructure.Data;

/// <summary>
/// SQLite context for local/mobile persistence.
/// Inherits all DbSets and model configuration from HealthAppDbContext.
/// </summary>
public class LocalDbContext : HealthAppDbContext
{
    public LocalDbContext(DbContextOptions<LocalDbContext> options)
        : base(ConvertOptions(options)) { }

    private static DbContextOptions<HealthAppDbContext> ConvertOptions(
        DbContextOptions<LocalDbContext> options)
    {
        var builder = new DbContextOptionsBuilder<HealthAppDbContext>();
        foreach (var extension in options.Extensions)
            ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(extension);
        return builder.Options;
    }
}
