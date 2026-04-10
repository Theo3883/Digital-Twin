using DigitalTwin.Mobile.Application.Services;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Services;
using DigitalTwin.Mobile.Infrastructure.Data;
using DigitalTwin.Mobile.Infrastructure.Repositories;
using DigitalTwin.Mobile.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Engine;

/// <summary>
/// Dependency injection setup for mobile engine
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMobileServices(this IServiceCollection services, string databasePath, string apiBaseUrl, string? geminiApiKey = null, string? openWeatherApiKey = null, string? googleOAuthClientId = null)
    {
        // ── Database (raw ADO.NET — no EF Core model building at runtime) ─────
        var connectionString = $"Data Source={databasePath}";
        services.AddSingleton(new SqliteConnectionFactory(connectionString));

        // ── Repositories (Infrastructure) ────────────────────────────────────
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IVitalSignRepository, VitalSignRepository>();
        services.AddScoped<IMedicationRepository, MedicationRepository>();
        services.AddScoped<IEnvironmentReadingRepository, EnvironmentReadingRepository>();
        services.AddScoped<ISleepSessionRepository, SleepSessionRepository>();
        services.AddScoped<IOcrDocumentRepository, OcrDocumentRepository>();
        services.AddScoped<IMedicalHistoryEntryRepository, MedicalHistoryEntryRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<ILocalDataResetService, LocalDataResetService>();
        services.AddSingleton<IAccessTokenStore, InMemoryAccessTokenStore>();

        // ── HTTP Clients ─────────────────────────────────────────────────────
        services.AddHttpClient<ICloudSyncService, CloudSyncService>(client =>
        {
            if (TryGetValidAbsoluteHttpBaseUri(apiBaseUrl, out var uri))
                client.BaseAddress = uri;
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // RxNav (public API, no key)
        services.AddHttpClient<IDrugSearchProvider, RxNavDrugSearchService>(client =>
        {
            client.BaseAddress = new Uri("https://rxnav.nlm.nih.gov/REST/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // Named clients for openFDA interaction service
        services.AddHttpClient("RxNav", client =>
        {
            client.BaseAddress = new Uri("https://rxnav.nlm.nih.gov/REST/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddHttpClient("OpenFda", client =>
        {
            client.BaseAddress = new Uri("https://api.fda.gov/drug/label.json");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddScoped<IMedicationInteractionProvider, OpenFdaInteractionService>();

        // Gemini AI
        var geminiKey = geminiApiKey ?? "";
        services.AddHttpClient("Gemini", client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IChatBotProvider>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("Gemini");
            var logger = sp.GetRequiredService<ILogger<GeminiChatService>>();
            return new GeminiChatService(client, geminiKey, logger);
        });
        services.AddScoped<ICoachingProvider>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("Gemini");
            var logger = sp.GetRequiredService<ILogger<GeminiCoachingService>>();
            return new GeminiCoachingService(client, geminiKey, logger);
        });

        // OpenWeather (weather + air quality)
        var weatherKey = openWeatherApiKey ?? "";
        services.AddHttpClient("OpenWeather", client =>
        {
            client.BaseAddress = new Uri("https://api.openweathermap.org/data/2.5/weather");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddHttpClient("OpenWeatherAQ", client =>
        {
            client.BaseAddress = new Uri("https://api.openweathermap.org/data/2.5/air_pollution");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddScoped<IEnvironmentDataProvider>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<EnvironmentDataService>>();
            return new EnvironmentDataService(factory, weatherKey, logger);
        });

        // ── Domain Services ──────────────────────────────────────────────────
        services.AddScoped<SyncService>();
        services.AddScoped<MedicationService>();
        services.AddScoped<MedicationInteractionService>();
        services.AddScoped<EnvironmentAssessmentService>();
        services.AddScoped<EcgTriageEngine>();
        services.AddScoped<IEcgTriageRule, HeartRateActivityRule>();
        services.AddScoped<IEcgTriageRule, SpO2Rule>();
        services.AddScoped<IEcgTriageRule, SignalQualityRule>();

        // ── Google token validation (client-side, no backend needed) ────────
        var googleClientId = googleOAuthClientId ?? "";
        services.AddHttpClient<GoogleTokenValidationService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<GoogleTokenValidationService>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient(nameof(GoogleTokenValidationService));
            var logger = sp.GetRequiredService<ILogger<GoogleTokenValidationService>>();
            return new GoogleTokenValidationService(client, googleClientId, logger);
        });

        // ── Application Services ─────────────────────────────────────────────
        services.AddScoped<AuthService>();
        services.AddScoped<PatientService>();
        services.AddScoped<VitalSignsService>();
        services.AddScoped<MedicationApplicationService>();
        services.AddScoped<EnvironmentApplicationService>();
        services.AddScoped<EcgApplicationService>();
        services.AddScoped<ChatBotApplicationService>();
        services.AddScoped<CoachingApplicationService>();
        services.AddScoped<SleepApplicationService>();
        services.AddScoped<OcrTextProcessingApplicationService>();

        // ── New application services ─────────────────────────────────────────
        services.AddScoped<DoctorAssignmentApplicationService>();
        services.AddScoped<EnvironmentAnalyticsApplicationService>();

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

    private static bool TryGetValidAbsoluteHttpBaseUri(string? value, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) return false;
        if (string.IsNullOrWhiteSpace(parsed.Host)) return false;
        uri = parsed;
        return true;
    }
}

/// <summary>
/// Database initialization service.
/// Uses raw DDL instead of EnsureCreatedAsync to avoid design-time model
/// building which is unsupported under NativeAOT.
/// </summary>
public class DatabaseInitializer
{
    private readonly SqliteConnectionFactory _db;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(SqliteConnectionFactory db, ILogger<DatabaseInitializer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("[DatabaseInitializer] Ensuring database is created");

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        // WAL mode for better concurrent read performance
        await ExecuteAsync(conn, "PRAGMA journal_mode = 'wal';");

        await ExecuteAsync(conn,"""
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

        await ExecuteAsync(conn,"""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Email"
            ON "Users" ("Email");
            """);

        await ExecuteAsync(conn,"""
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

        await ExecuteAsync(conn,"""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Patients_UserId"
            ON "Patients" ("UserId");
            """);

        await ExecuteAsync(conn,"""
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

        await ExecuteAsync(conn,"""
            CREATE INDEX IF NOT EXISTS "IX_VitalSigns_PatientId_Type_Timestamp"
            ON "VitalSigns" ("PatientId", "Type", "Timestamp");
            """);

        // ── Medications ────────────────────────────────────────────────────
        await ExecuteAsync(conn,"""
            CREATE TABLE IF NOT EXISTS "Medications" (
                "Id"                TEXT NOT NULL,
                "PatientId"         TEXT NOT NULL,
                "Name"              TEXT NOT NULL,
                "Dosage"            TEXT NOT NULL,
                "Frequency"         TEXT NULL,
                "Route"             INTEGER NOT NULL,
                "RxCui"             TEXT NULL,
                "Instructions"      TEXT NULL,
                "Reason"            TEXT NULL,
                "PrescribedByUserId" TEXT NULL,
                "StartDate"         TEXT NULL,
                "EndDate"           TEXT NULL,
                "Status"            INTEGER NOT NULL,
                "DiscontinuedReason" TEXT NULL,
                "AddedByRole"       INTEGER NOT NULL,
                "CreatedAt"         TEXT NOT NULL,
                "UpdatedAt"         TEXT NOT NULL,
                "IsSynced"          INTEGER NOT NULL,
                CONSTRAINT "PK_Medications" PRIMARY KEY ("Id")
            );
            """);

        await ExecuteAsync(conn,"""
            CREATE INDEX IF NOT EXISTS "IX_Medications_PatientId_Status"
            ON "Medications" ("PatientId", "Status");
            """);

        // ── EnvironmentReadings ────────────────────────────────────────────
        await ExecuteAsync(conn,"""
            CREATE TABLE IF NOT EXISTS "EnvironmentReadings" (
                "Id"                  TEXT NOT NULL,
                "Latitude"            REAL NOT NULL,
                "Longitude"           REAL NOT NULL,
                "LocationDisplayName" TEXT NULL,
                "PM25"                REAL NOT NULL,
                "PM10"                REAL NOT NULL,
                "O3"                  REAL NOT NULL,
                "NO2"                 REAL NOT NULL,
                "Temperature"         REAL NOT NULL,
                "Humidity"            REAL NOT NULL,
                "AirQuality"          INTEGER NOT NULL,
                "AqiIndex"            INTEGER NOT NULL,
                "Timestamp"           TEXT NOT NULL,
                "IsDirty"             INTEGER NOT NULL,
                "SyncedAt"            TEXT NULL,
                CONSTRAINT "PK_EnvironmentReadings" PRIMARY KEY ("Id")
            );
            """);

        await ExecuteAsync(conn,"""
            CREATE INDEX IF NOT EXISTS "IX_EnvironmentReadings_Timestamp"
            ON "EnvironmentReadings" ("Timestamp");
            """);

        // ── SleepSessions ──────────────────────────────────────────────────
        await ExecuteAsync(conn,"""
            CREATE TABLE IF NOT EXISTS "SleepSessions" (
                "Id"              TEXT NOT NULL,
                "PatientId"       TEXT NOT NULL,
                "StartTime"       TEXT NOT NULL,
                "EndTime"         TEXT NOT NULL,
                "DurationMinutes" INTEGER NOT NULL,
                "QualityScore"    REAL NOT NULL,
                "CreatedAt"       TEXT NOT NULL,
                "IsSynced"        INTEGER NOT NULL,
                CONSTRAINT "PK_SleepSessions" PRIMARY KEY ("Id")
            );
            """);

        await ExecuteAsync(conn,"""
            CREATE INDEX IF NOT EXISTS "IX_SleepSessions_PatientId_StartTime"
            ON "SleepSessions" ("PatientId", "StartTime");
            """);

        // ── OcrDocuments ───────────────────────────────────────────────────
        await ExecuteAsync(conn,"""
            CREATE TABLE IF NOT EXISTS "OcrDocuments" (
                "Id"                  TEXT NOT NULL,
                "PatientId"           TEXT NOT NULL,
                "OpaqueInternalName"  TEXT NOT NULL,
                "MimeType"            TEXT NULL,
                "PageCount"           INTEGER NOT NULL,
                "Sha256OfNormalized"  TEXT NULL,
                "SanitizedOcrPreview" TEXT NULL,
                "EncryptedVaultPath"  TEXT NULL,
                "ScannedAt"           TEXT NOT NULL,
                "CreatedAt"           TEXT NOT NULL,
                "UpdatedAt"           TEXT NOT NULL,
                "IsDirty"             INTEGER NOT NULL,
                "SyncedAt"            TEXT NULL,
                CONSTRAINT "PK_OcrDocuments" PRIMARY KEY ("Id")
            );
            """);

        await ExecuteAsync(conn,"""
            CREATE INDEX IF NOT EXISTS "IX_OcrDocuments_PatientId_IsDirty"
            ON "OcrDocuments" ("PatientId", "IsDirty");
            """);

        // ── MedicalHistoryEntries ──────────────────────────────────────────
        await ExecuteAsync(conn,"""
            CREATE TABLE IF NOT EXISTS "MedicalHistoryEntries" (
                "Id"               TEXT NOT NULL,
                "PatientId"        TEXT NOT NULL,
                "SourceDocumentId" TEXT NOT NULL,
                "Title"            TEXT NULL,
                "MedicationName"   TEXT NULL,
                "Dosage"           TEXT NULL,
                "Frequency"        TEXT NULL,
                "Duration"         TEXT NULL,
                "Notes"            TEXT NULL,
                "Summary"          TEXT NULL,
                "Confidence"       TEXT NOT NULL,
                "EventDate"        TEXT NOT NULL,
                "CreatedAt"        TEXT NOT NULL,
                "UpdatedAt"        TEXT NOT NULL,
                "IsDirty"          INTEGER NOT NULL,
                "SyncedAt"         TEXT NULL,
                CONSTRAINT "PK_MedicalHistoryEntries" PRIMARY KEY ("Id")
            );
            """);

        await ExecuteAsync(conn,"""
            CREATE INDEX IF NOT EXISTS "IX_MedicalHistoryEntries_PatientId"
            ON "MedicalHistoryEntries" ("PatientId");
            """);

        await ExecuteAsync(conn,"""
            CREATE INDEX IF NOT EXISTS "IX_MedicalHistoryEntries_SourceDocumentId"
            ON "MedicalHistoryEntries" ("SourceDocumentId");
            """);

        // ── ChatMessages ───────────────────────────────────────────────────
        await ExecuteAsync(conn,"""
            CREATE TABLE IF NOT EXISTS "ChatMessages" (
                "Id"        TEXT NOT NULL,
                "Content"   TEXT NOT NULL,
                "IsUser"    INTEGER NOT NULL,
                "Timestamp" TEXT NOT NULL,
                CONSTRAINT "PK_ChatMessages" PRIMARY KEY ("Id")
            );
            """);

        _logger.LogInformation("[DatabaseInitializer] Database initialization complete");
    }

    private static async Task ExecuteAsync(Microsoft.Data.Sqlite.SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}