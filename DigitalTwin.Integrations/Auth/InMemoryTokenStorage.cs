using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;

namespace DigitalTwin.Integrations.Auth;

/// <summary>
/// Non-persistent, in-memory token storage.
/// Used in unit tests and on platforms that don't support <c>SecureStorage</c>.
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
