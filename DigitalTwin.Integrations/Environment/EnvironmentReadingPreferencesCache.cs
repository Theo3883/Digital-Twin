using System.Text.Json;
using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using Microsoft.Maui.Storage;

namespace DigitalTwin.Integrations.Environment;

/// <summary>
/// Stores the last environment snapshot as JSON in MAUI preferences.
/// </summary>
public sealed class EnvironmentReadingPreferencesCache : IEnvironmentReadingCache
{
    private const string Key = "env_reading_snapshot_json_v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public EnvironmentReadingDto? GetLastOrDefault()
    {
        var json = Preferences.Get(Key, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<EnvironmentReadingDto>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(EnvironmentReadingDto reading)
    {
        var json = JsonSerializer.Serialize(reading, JsonOptions);
        Preferences.Set(Key, json);
    }
}
