using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DigitalTwin.Mobile.Infrastructure.Data;

/// <summary>
/// Design-time factory used by EF Core tooling (migrations, compiled models, etc.)
/// </summary>
public class MobileDbContextDesignFactory : IDesignTimeDbContextFactory<MobileDbContext>
{
    public MobileDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MobileDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        return new MobileDbContext(options);
    }
}
