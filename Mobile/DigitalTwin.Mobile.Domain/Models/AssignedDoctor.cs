namespace DigitalTwin.Mobile.Domain.Models;

public sealed record AssignedDoctor
{
    public Guid DoctorId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public DateTime AssignedAt { get; init; }
    public string? Notes { get; init; }
}
