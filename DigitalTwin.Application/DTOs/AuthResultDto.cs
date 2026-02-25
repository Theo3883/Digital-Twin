namespace DigitalTwin.Application.DTOs;

/// <summary>Immutable authentication result returned after a successful sign-in or registration.</summary>
public record AuthResultDto
{
    public long UserId { get; init; }
    public long PatientId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
}

