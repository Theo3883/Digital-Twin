namespace DigitalTwin.Application.DTOs;

public class AssignedDoctorDto
{
    public Guid DoctorId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public DateTime AssignedAt { get; set; }
    public string? Notes { get; set; }
}

