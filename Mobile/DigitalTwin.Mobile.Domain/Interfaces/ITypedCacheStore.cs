using System.Text.Json.Serialization.Metadata;

namespace DigitalTwin.Mobile.Domain.Interfaces;

public sealed record CacheEnvelope<T>
{
    public required T Value { get; init; }
    public DateTime StoredAtUtc { get; init; }
    public string? Fingerprint { get; init; }
}

public interface ITypedCacheStore
{
    Task<CacheEnvelope<T>?> GetAsync<T>(string key, JsonTypeInfo<T> typeInfo, CancellationToken ct = default);

    Task SetAsync<T>(string key, CacheEnvelope<T> envelope, JsonTypeInfo<T> typeInfo, CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);

    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);

    Task DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default);
}
