using DigitalTwin.Application.Interfaces;

namespace DigitalTwin.Platform.Auth;

public class SecureTokenStorage : ISecureTokenStorage
{
    public async Task StoreAsync(string key, string value)
    {
        await SecureStorage.Default.SetAsync(key, value);
    }

    public Task<string?> GetAsync(string key)
    {
        return SecureStorage.Default.GetAsync(key);
    }

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
