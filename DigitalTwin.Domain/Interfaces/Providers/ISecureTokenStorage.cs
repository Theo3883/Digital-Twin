namespace DigitalTwin.Domain.Interfaces.Providers;

public interface ISecureTokenStorage
{
    Task StoreAsync(string key, string value);
    Task<string?> GetAsync(string key);
    Task RemoveAsync(string key);
    Task ClearAllAsync();
}
