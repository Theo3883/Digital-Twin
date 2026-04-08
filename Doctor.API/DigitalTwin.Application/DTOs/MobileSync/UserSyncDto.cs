using DigitalTwin.Domain.Enums;

namespace DigitalTwin.Application.DTOs.MobileSync;

/// <summary>
/// User data for mobile sync operations.
/// </summary>
public record UserSyncDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

/// <summary>
/// Request to upsert user data to cloud.
/// </summary>
public record UpsertUserRequest : MobileSyncRequestBase
{
    public UserSyncDto User { get; init; } = new();
}

/// <summary>
/// Response from user upsert operation.
/// </summary>
public record UpsertUserResponse : MobileSyncResponseBase
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? CloudUserId { get; init; }
}