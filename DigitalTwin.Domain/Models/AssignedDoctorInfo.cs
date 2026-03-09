namespace DigitalTwin.Domain.Models;

public class AssignedDoctorInfo
{
    public User Doctor { get; set; } = new();
    public DateTime AssignedAt { get; set; }
    public string? Notes { get; set; }
}

