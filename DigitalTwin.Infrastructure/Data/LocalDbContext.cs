using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Data;

/// <summary>
/// SQLite context for local/mobile persistence.
/// Inherits all DbSets and model configuration from HealthAppDbContext.
/// </summary>
public class LocalDbContext : HealthAppDbContext
{
    public LocalDbContext(DbContextOptions<LocalDbContext> options)
        : base(ConvertOptions(options)) { }
}
