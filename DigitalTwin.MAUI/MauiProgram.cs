using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using DigitalTwin.Application.Configuration;
using DigitalTwin.Composition;
using DigitalTwin.Integrations;
using DigitalTwin.Integrations.Sync;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.OCR;
using DigitalTwin.OCR.Models.Enums;
using DigitalTwin.Services;
using DigitalTwin.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
#if IOS
using Foundation;
using Microsoft.AspNetCore.Components.WebView.Maui;
#endif
// Composition is the single DI entry point — MAUI passes integrations via callback.

namespace DigitalTwin;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        LoadEnv();

        var config = EnvConfig.FromEnvironment();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            })
#if IOS
            // Register our custom handler that returns a NoAccessoryWebView subclass,
            // suppressing the iOS keyboard accessory bar (▲ ▼ ✓) natively on all devices.
            .ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<Microsoft.AspNetCore.Components.WebView.Maui.BlazorWebView,
                                    NoAccessoryBlazorWebViewHandler>();
            })
#endif
            ;


        builder.Services.AddMauiBlazorWebView();

        builder.Services.AddMudServices();

        builder.Services.AddSingleton<IAppRouteState, AppRouteState>();
        builder.Services.AddSingleton<IPullRefreshCoordinator, PullRefreshCoordinator>();
        builder.Services.AddSingleton<ChatHistoryStore>();
        builder.Services.AddSingleton<MainPage>();

        var localDbPath = Path.Combine(FileSystem.AppDataDirectory, "healthapp.db");
        System.Diagnostics.Debug.WriteLine($"[DB PATH] {localDbPath}");
        
        builder.Services.AddDigitalTwinForMaui(
            localConnectionString: $"Data Source={localDbPath}",
            cloudConnectionString: config.PostgresConnectionString,
            registerIntegrations: svc =>
            {
                svc.AddIntegrations(config);
                svc.AddDigitalTwinOcr(opts =>
                {
#if DEBUG
                    opts.SecurityMode = SecurityMode.RelaxedDebug;
#else
                    opts.SecurityMode = SecurityMode.Strict;
#endif

                    // ML feature flags are off by default. Enable via .env without changing code:
                    //   OCR_USE_ML_CLASSIFICATION=1
                    //   OCR_USE_ML_EXTRACTION=1
                    //   OCR_ML_CONFIDENCE_THRESHOLD=0.65
                    opts.UseMlClassification = Environment.GetEnvironmentVariable("OCR_USE_ML_CLASSIFICATION") == "1";
                    opts.UseMlExtraction = Environment.GetEnvironmentVariable("OCR_USE_ML_EXTRACTION") == "1";
                    if (float.TryParse(
                            Environment.GetEnvironmentVariable("OCR_ML_CONFIDENCE_THRESHOLD"),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var threshold))
                    {
                        opts.MlConfidenceThreshold = threshold;
                    }
                });
                
                svc.AddDocumentPreviewService();
                svc.AddLocationSearchService();
            });

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
        builder.Logging.AddFilter("DigitalTwin.OCR", LogLevel.Debug);
#endif

        var app = builder.Build();
        ApplyDatabaseMigrations(app);

        if (!config.UseGeminiAi)
        {
            var startupLog = app.Services.GetRequiredService<ILogger<MauiApp>>();
            startupLog.LogWarning(
                "[Gemini] GEMINI_API_KEY is not configured — ChatBot is running in mock mode. " +
                "Add GEMINI_API_KEY=<your-key> to your .env file at the project root to enable real Gemini AI responses.");
        }

        _ = app.Services.GetRequiredService<ConnectivityMonitor>();
        return app;
    }

    private static void ApplyDatabaseMigrations(MauiApp app)
    {
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            var startupLog = app.Services.GetService<ILogger<MauiApp>>();
            startupLog?.LogWarning("[Database] Skipping EF Core runtime migrations because dynamic code is unavailable in this iOS runtime.");
            return;
        }

        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        // With AddDbContextFactory, DbContext is no longer in DI as a service —
        // use the factory to create a short-lived instance just for migrations.
        var localFactory = services.GetRequiredService<IDbContextFactory<LocalDbContext>>();
        using var localDb = localFactory.CreateDbContext();

        localDb.Database.Migrate();

        // Cloud (PostgreSQL) migrations are run via `dotnet ef database update` — not at runtime.
    }

    private static void LoadEnv()
    {
        var secretsPath = FindSecretsFile();
        if (!string.IsNullOrWhiteSpace(secretsPath))
        {
            LoadEnvironmentVariables(secretsPath);
        }

        LoadOAuthFromClientPlist();
    }

    private static string? FindSecretsFile()
    {
#if IOS
        var bundledSecretsPath = Path.Combine(NSBundle.MainBundle.BundlePath, "build-secrets.env");
        if (File.Exists(bundledSecretsPath))
        {
            return bundledSecretsPath;
        }

        var bundledPath = Path.Combine(NSBundle.MainBundle.BundlePath, ".env");
        if (File.Exists(bundledPath))
        {
            return bundledPath;
        }
#endif

        var currentDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (File.Exists(currentDirectoryPath))
        {
            return currentDirectoryPath;
        }

        var baseDirectoryPath = Path.Combine(AppContext.BaseDirectory, ".env");
        if (File.Exists(baseDirectoryPath))
        {
            return baseDirectoryPath;
        }

        var relativeBasePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".env");
        return File.Exists(relativeBasePath) ? relativeBasePath : null;
    }

    private static void LoadEnvironmentVariables(string path)
    {
        string[] lines;

        try
        {
            lines = File.ReadAllLines(path);
        }
        catch
        {
            return;
        }

        foreach (var rawLine in lines)
        {
            var line = NormalizeEnvLine(rawLine);
            if (line is null) continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0) continue;

            var key = line.Substring(0, separatorIndex).Trim();
            var value = line.Substring(separatorIndex + 1).Trim();

            if (!string.IsNullOrWhiteSpace(key))
                Environment.SetEnvironmentVariable(key, TrimQuotes(value));
        }
    }

    private static string? NormalizeEnvLine(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine)) return null;

        var line = rawLine.Trim();
        if (line.StartsWith("export ", StringComparison.Ordinal))
            line = line.Substring("export ".Length).Trim();

        return string.IsNullOrWhiteSpace(line) || line.StartsWith('#') ? null : line;
    }

    private static string TrimQuotes(string value)
    {
        if (value.Length >= 2)
        {
            var first = value[0];
            var last = value[value.Length - 1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            {
                return value.Substring(1, value.Length - 2);
            }
        }

        return value;
    }

    private static void LoadOAuthFromClientPlist()
    {
        var hasClientId = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_ID"));
        var hasRedirectUri = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_OAUTH_REDIRECT_URI"));
        if (hasClientId && hasRedirectUri)
        {
            return;
        }

        SearchAndApplyGoogleOAuthFromPlist(hasClientId, hasRedirectUri);
    }

    private static void SearchAndApplyGoogleOAuthFromPlist(bool hasClientId, bool hasRedirectUri)
    {
        var searchPatterns = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "client_*.plist"),
            Path.Combine(AppContext.BaseDirectory, "client_*.plist"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "client_*.plist")
        };

        _ = searchPatterns.FirstOrDefault(p => TryApplyFromDirectory(p, hasClientId, hasRedirectUri));
    }

    private static bool TryApplyFromDirectory(string pattern, bool hasClientId, bool hasRedirectUri)
    {
        var directory = Path.GetDirectoryName(pattern);
        var filePattern = Path.GetFileName(pattern);

        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(filePattern) || !Directory.Exists(directory))
            return false;

        foreach (var file in Directory.GetFiles(directory, filePattern, SearchOption.TopDirectoryOnly))
        {
            if (!TryLoadGoogleOAuthFromPlist(file, out var clientId, out var reversedClientId))
                continue;

            if (!hasClientId && !string.IsNullOrWhiteSpace(clientId))
                Environment.SetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_ID", clientId);

            if (!hasRedirectUri && !string.IsNullOrWhiteSpace(reversedClientId))
                Environment.SetEnvironmentVariable("GOOGLE_OAUTH_REDIRECT_URI", $"{reversedClientId}:/oauthredirect");

            return true;
        }

        return false;
    }

    private static bool TryLoadGoogleOAuthFromPlist(string path, out string? clientId, out string? reversedClientId)
    {
        clientId = null;
        reversedClientId = null;

        try
        {
            var doc = XDocument.Load(path);
            var dict = doc.Root?.Element("dict");
            if (dict is null)
            {
                return false;
            }

            clientId = GetPlistValue(dict, "CLIENT_ID");
            reversedClientId = GetPlistValue(dict, "REVERSED_CLIENT_ID");

            return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(reversedClientId);
        }
        catch
        {
            return false;
        }
    }

    private static string? GetPlistValue(XElement dict, string key)
    {
        var elements = dict.Elements().ToList();

        for (var index = 0; index < elements.Count - 1; index++)
        {
            var current = elements[index];
            if (current.Name.LocalName != "key" || !string.Equals(current.Value, key, StringComparison.Ordinal))
            {
                continue;
            }

            var valueElement = elements[index + 1];
            return valueElement.Name.LocalName == "string" ? valueElement.Value : null;
        }

        return null;
    }
}
