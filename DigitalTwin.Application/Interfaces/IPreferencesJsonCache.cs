namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Keyed JSON persistence in app preferences (MAUI <see cref="Microsoft.Maui.Storage.Preferences"/>).
/// </summary>
public interface IPreferencesJsonCache
{
    T? Get<T>(string key);

    void Set<T>(string key, T value);

    void Remove(string key);
}
