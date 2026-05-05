namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Keyed JSON persistence in app preferences (platform-specific implementation on mobile).
/// </summary>
public interface IPreferencesJsonCache
{
    T? Get<T>(string key);

    void Set<T>(string key, T value);

    void Remove(string key);
}
