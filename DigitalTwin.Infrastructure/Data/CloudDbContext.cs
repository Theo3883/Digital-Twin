using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DigitalTwin.Infrastructure.Data;

/// <summary>
/// PostgreSQL context for cloud persistence.
/// Inherits all DbSets and model configuration from HealthAppDbContext.
/// </summary>
public class CloudDbContext : HealthAppDbContext
{
    public CloudDbContext(DbContextOptions<CloudDbContext> options)
        : base(ConvertOptions(options)) { }

    private static DbContextOptions<HealthAppDbContext> ConvertOptions(
        DbContextOptions<CloudDbContext> options)
    {
        var builder = new DbContextOptionsBuilder<HealthAppDbContext>();
        foreach (var extension in options.Extensions)
            ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(extension);
        return builder.Options;
    }
}
