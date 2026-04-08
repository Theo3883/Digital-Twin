using DigitalTwin.Mobile.Application.Services;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Services;
using DigitalTwin.Mobile.Infrastructure.Data;
using DigitalTwin.Mobile.Infrastructure.Repositories;
using DigitalTwin.Mobile.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Engine;

/// <summary>
/// Dependency injection setup for mobile engine
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMobileServices(this IServiceCollection services, string databasePath, string apiBaseUrl)
    {
        // ── Database ──────────────────────────────────────────────────────────
        services.AddDbContext<MobileDbContext>(options =>
        {
            options.UseSqlite($"Data Source={databasePath}");
        });

        // ── Repositories (Infrastructure) ────────────────────────────────────
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IVitalSignRepository, VitalSignRepository>();

        // ── HTTP Client for Cloud Sync ───────────────────────────────────────
        services.AddHttpClient<ICloudSyncService, CloudSyncService>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // ── Domain Services ──────────────────────────────────────────────────
        services.AddScoped<SyncService>();

        // ── Application Services ─────────────────────────────────────────────
        services.AddScoped<AuthService>();
        services.AddScoped<PatientService>();
        services.AddScoped<VitalSignsService>();

        // ── Infrastructure Services ──────────────────────────────────────────
        services.AddScoped<DatabaseInitializer>();

        // ── Logging ───────────────────────────────────────────────────────────
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        return services;
    }
}

/// <summary>
/// Database initialization service
/// </summary>
public class DatabaseInitializer
{
    private readonly MobileDbContext _context;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(MobileDbContext context, ILogger<DatabaseInitializer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("[DatabaseInitializer] Ensuring database is created");
        await _context.Database.EnsureCreatedAsync();
        _logger.LogInformation("[DatabaseInitializer] Database initialization complete");
    }
}