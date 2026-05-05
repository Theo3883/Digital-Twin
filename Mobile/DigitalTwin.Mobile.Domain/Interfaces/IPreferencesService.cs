namespace DigitalTwin.Mobile.Domain.Interfaces;

/// <summary>
/// Simple key-value preferences service for persisting app state
/// </summary>
public interface IPreferencesService
{
    /// <summary>
    /// Get a preference value by key
    /// </summary>
    Task<string?> GetAsync(string key);

    /// <summary>
    /// Set a preference value
    /// </summary>
    Task SetAsync(string key, string? value);

    /// <summary>
    /// Remove a preference key
    /// </summary>
    Task RemoveAsync(string key);

    /// <summary>
    /// Check if a key exists
    /// </summary>
    Task<bool> ExistsAsync(string key);
}
