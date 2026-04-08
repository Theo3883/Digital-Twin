namespace DigitalTwin.Application.DTOs.MobileSync;

/// <summary>
/// Base class for all mobile sync responses providing server timestamp and request tracking.
/// </summary>
public abstract record MobileSyncResponseBase
{
    /// <summary>
    /// UTC timestamp when the response was generated on the server.
    /// </summary>
    public DateTime ServerTimeUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Echo of the request ID for correlation.
    /// </summary>
    public string RequestId { get; init; } = string.Empty;
}