using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;

namespace DigitalTwin.Integrations.Environment;

/// <summary>
/// Stores the last environment snapshot via <see cref="IPreferencesJsonCache"/>.
/// </summary>
public sealed class EnvironmentReadingPreferencesCache : IEnvironmentReadingCache
{
    public const string Key = "env_reading_snapshot_json_v1";

    private readonly IPreferencesJsonCache _prefs;

    public EnvironmentReadingPreferencesCache(IPreferencesJsonCache prefs)
    {
        _prefs = prefs;
    }

    public EnvironmentReadingDto? GetLastOrDefault() =>
        _prefs.Get<EnvironmentReadingDto>(Key);

    public void Save(EnvironmentReadingDto reading) =>
        _prefs.Set(Key, reading);
}
