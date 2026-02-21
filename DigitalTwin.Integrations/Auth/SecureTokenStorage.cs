using DigitalTwin.Application.Interfaces;

namespace DigitalTwin.Integrations.Auth;

/// <summary>
/// In-memory fallback for non-MAUI environments.
/// The MAUI project registers a platform-specific SecureStorage wrapper.
/// </summary>
public class InMemoryTokenStorage : ISecureTokenStorage
{
    private readonly Dictionary<string, string> _store = new();

    public Task StoreAsync(string key, string value)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key)
    {
        _store.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public Task RemoveAsync(string key)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }

    public Task ClearAllAsync()
    {
        _store.Clear();
        return Task.CompletedTask;
    }
}
