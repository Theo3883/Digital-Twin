namespace DigitalTwin.Application.DTOs.MobileSync;

/// <summary>
/// Base class for all mobile sync requests providing idempotency and device tracking.
/// </summary>
public abstract record MobileSyncRequestBase
{
    /// <summary>
    /// Stable device identifier for this app installation.
    /// </summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Unique identifier for this specific request to ensure idempotency.
    /// </summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the request was generated on the client.
    /// </summary>
    public DateTime ClientGeneratedAtUtc { get; init; } = DateTime.UtcNow;
}