using System.Text.Json;
using DigitalTwin.Application.Interfaces;
using Microsoft.Maui.Storage;

namespace DigitalTwin.Integrations.Caching;

/// <summary>
/// Generic JSON get/set for MAUI preferences.
/// </summary>
public sealed class PreferencesJsonCache : IPreferencesJsonCache
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public T? Get<T>(string key)
    {
        var json = Preferences.Get(key, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return default;
        }
    }

    public void Set<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        Preferences.Set(key, json);
    }

    public void Remove(string key) => Preferences.Remove(key);
}
