#if IOS || MACCATALYST
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;

namespace DigitalTwin.Integrations.Auth;

/// <summary>
/// MAUI platform implementation that delegates to iOS/Android <c>SecureStorage</c>
/// (Keychain on iOS, EncryptedSharedPreferences on Android).
/// Compiled only for mobile platform targets; <see cref="InMemoryTokenStorage"/>
/// is used on non-platform builds.
/// </summary>
public class SecureTokenStorage : ISecureTokenStorage
{
    public async Task StoreAsync(string key, string value)
        => await SecureStorage.Default.SetAsync(key, value);

    public Task<string?> GetAsync(string key)
        => SecureStorage.Default.GetAsync(key);

    public Task RemoveAsync(string key)
    {
        SecureStorage.Default.Remove(key);
        return Task.CompletedTask;
    }

    public Task ClearAllAsync()
    {
        SecureStorage.Default.RemoveAll();
        return Task.CompletedTask;
    }
}
#endif

