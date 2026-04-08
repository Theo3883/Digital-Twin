using DigitalTwin.Mobile.Application.Services;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Services;
using DigitalTwin.Mobile.Infrastructure.Data;
using DigitalTwin.Mobile.Infrastructure.Data.CompiledModels;
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
            options.UseModel(MobileDbContextModel.Instance);
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
/// Database initialization service.
/// Uses raw DDL instead of EnsureCreatedAsync to avoid design-time model
/// building which is unsupported under NativeAOT.
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

        var db = _context.Database;

        // WAL mode for better concurrent read performance
        await db.ExecuteSqlRawAsync("PRAGMA journal_mode = 'wal';");

        await db.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Users" (
                "Id"          TEXT NOT NULL,
                "CreatedAt"   TEXT NOT NULL,
                "DateOfBirth" TEXT NULL,
                "Email"       TEXT NOT NULL,
                "FirstName"   TEXT NULL,
                "IsSynced"    INTEGER NOT NULL,
                "LastName"    TEXT NULL,
                "Phone"       TEXT NULL,
                "PhotoUrl"    TEXT NULL,
                "Role"        INTEGER NOT NULL,
                "UpdatedAt"   TEXT NOT NULL,
                CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
            );
            """);

        await db.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Email"
            ON "Users" ("Email");
            """);

        await db.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Patients" (
                "Id"                     TEXT NOT NULL,
                "Allergies"              TEXT NULL,
                "BloodPressureDiastolic" INTEGER NULL,
                "BloodPressureSystolic"  INTEGER NULL,
                "BloodType"              TEXT NULL,
                "Cholesterol"            REAL NULL,
                "Cnp"                    TEXT NULL,
                "CreatedAt"              TEXT NOT NULL,
                "Height"                 REAL NULL,
                "IsSynced"               INTEGER NOT NULL,
                "MedicalHistoryNotes"    TEXT NULL,
                "UpdatedAt"              TEXT NOT NULL,
                "UserId"                 TEXT NOT NULL,
                "Weight"                 REAL NULL,
                CONSTRAINT "PK_Patients" PRIMARY KEY ("Id")
            );
            """);

        await db.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Patients_UserId"
            ON "Patients" ("UserId");
            """);

        await db.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "VitalSigns" (
                "Id"        TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "IsSynced"  INTEGER NOT NULL,
                "PatientId" TEXT NOT NULL,
                "Source"    TEXT NOT NULL,
                "Timestamp" TEXT NOT NULL,
                "Type"      INTEGER NOT NULL,
                "Unit"      TEXT NOT NULL,
                "Value"     REAL NOT NULL,
                CONSTRAINT "PK_VitalSigns" PRIMARY KEY ("Id")
            );
            """);

        await db.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_VitalSigns_PatientId_Type_Timestamp"
            ON "VitalSigns" ("PatientId", "Type", "Timestamp");
            """);

        _logger.LogInformation("[DatabaseInitializer] Database initialization complete");
    }
}