namespace DigitalTwin.Application.DTOs;

public class AuthResultDto
{
    public long UserId { get; set; }
    public long PatientId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
}
