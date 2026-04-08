using DigitalTwin.Application.Enums;

namespace DigitalTwin.Application.DTOs.MobileSync;

/// <summary>
/// Vital sign data for mobile sync operations.
/// </summary>
public record VitalSignSyncDto
{
    public VitalSignType Type { get; init; }
    public double Value { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; }
}

/// <summary>
/// Request to append vital signs to cloud (time-series data).
/// </summary>
public record AppendVitalSignsRequest : MobileSyncRequestBase
{
    /// <summary>
    /// Cloud patient ID to append vitals for.
    /// </summary>
    public Guid PatientCloudId { get; init; }

    /// <summary>
    /// Vital signs to append.
    /// </summary>
    public List<VitalSignSyncDto> Items { get; init; } = new();
}

/// <summary>
/// Response from vital signs append operation.
/// </summary>
public record AppendVitalSignsResponse : MobileSyncResponseBase
{
    public int AcceptedCount { get; init; }
    public int DedupedCount { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Request to pull vital signs from cloud.
/// </summary>
public record GetVitalSignsRequest
{
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public VitalSignType? Type { get; init; }
}

/// <summary>
/// Response containing vital signs data from cloud.
/// </summary>
public record GetVitalSignsResponse : MobileSyncResponseBase
{
    public List<VitalSignSyncDto> Items { get; init; } = new();
}