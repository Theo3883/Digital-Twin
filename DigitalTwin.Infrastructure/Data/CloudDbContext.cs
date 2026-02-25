using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Data;

/// <summary>
/// PostgreSQL context for cloud persistence.
/// Inherits all DbSets and model configuration from HealthAppDbContext.
/// </summary>
public class CloudDbContext : HealthAppDbContext
{
    public CloudDbContext(DbContextOptions<CloudDbContext> options)
        : base(ConvertOptions(options)) { }
}
