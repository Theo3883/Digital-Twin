namespace DigitalTwin.Application.DTOs.MobileSync;

/// <summary>
/// Request for patient authentication via Google OAuth.
/// </summary>
public record PatientAuthRequest
{
    public string IdToken { get; init; } = string.Empty;
}

/// <summary>
/// Response from patient authentication.
/// </summary>
public record PatientAuthResponse
{
    public string Token { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? Name { get; init; }
    public Guid? UserId { get; init; }
    public Guid? PatientId { get; init; }
    public bool RequiresProfileSetup { get; init; }
}

/// <summary>
/// Response containing current user and patient information.
/// </summary>
public record GetMeResponse : MobileSyncResponseBase
{
    public Guid UserId { get; init; }
    public Guid? PatientId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
}