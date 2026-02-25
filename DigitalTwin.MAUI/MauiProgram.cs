using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using DigitalTwin.Application.Configuration;
using DigitalTwin.Composition;
using DigitalTwin.Integrations;
using DigitalTwin.Integrations.Sync;
using DigitalTwin.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;

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
            });

        builder.Services.AddMauiBlazorWebView();

        builder.Services.AddMudServices();

        var localDbPath = Path.Combine(FileSystem.AppDataDirectory, "healthapp.db");

        builder.Services.AddDigitalTwin(
            localConnectionString: $"Data Source={localDbPath}",
            cloudConnectionString: config.PostgresConnectionString);

        builder.Services.AddIntegrations(config);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        ApplyDatabaseMigrations(app);
        _ = app.Services.GetRequiredService<ConnectivityMonitor>();
        return app;
    }

    private static void ApplyDatabaseMigrations(MauiApp app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger("DatabaseMigrations");

        // With AddDbContextFactory, DbContext is no longer in DI as a service —
        // use the factory to create a short-lived instance just for migrations.
        var localFactory = services.GetRequiredService<IDbContextFactory<LocalDbContext>>();
        using var localDb = localFactory.CreateDbContext();
        localDb.Database.Migrate();

        var cloudFactory = services.GetService<IDbContextFactory<CloudDbContext>>();
        if (cloudFactory is null) return;

        try
        {
            using var cloudDb = cloudFactory.CreateDbContext();
            cloudDb.Database.Migrate();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Cloud database migration failed. App continues with local database.");
        }
    }

    private static void LoadEnv()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
            Path.Combine(AppContext.BaseDirectory, ".env"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".env"),
        };

        foreach (var path in candidates)
        {
            var normalized = Path.GetFullPath(path);
            if (File.Exists(normalized))
            {
                DotNetEnv.Env.Load(normalized);
                break;
            }
        }

        LoadOAuthFromClientPlist();
    }

    private static void LoadOAuthFromClientPlist()
    {
        var hasClientId = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_ID"));
        var hasRedirectUri = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_OAUTH_REDIRECT_URI"));
        if (hasClientId && hasRedirectUri)
        {
            return;
        }

        var candidateFiles = new List<string>
        {
            Path.Combine(Directory.GetCurrentDirectory(), "client_*.plist"),
            Path.Combine(AppContext.BaseDirectory, "client_*.plist"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "client_*.plist")
        };

        foreach (var pattern in candidateFiles)
        {
            var directory = Path.GetDirectoryName(pattern);
            var filePattern = Path.GetFileName(pattern);

            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(filePattern) || !Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.GetFiles(directory, filePattern, SearchOption.TopDirectoryOnly))
            {
                if (!TryLoadGoogleOAuthFromPlist(file, out var clientId, out var reversedClientId))
                {
                    continue;
                }

                if (!hasClientId && !string.IsNullOrWhiteSpace(clientId))
                {
                    Environment.SetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_ID", clientId);
                }

                if (!hasRedirectUri && !string.IsNullOrWhiteSpace(reversedClientId))
                {
                    Environment.SetEnvironmentVariable("GOOGLE_OAUTH_REDIRECT_URI", $"{reversedClientId}:/oauthredirect");
                }

                return;
            }
        }
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
