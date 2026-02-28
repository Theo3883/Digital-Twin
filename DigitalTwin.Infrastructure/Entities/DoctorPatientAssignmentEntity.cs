namespace DigitalTwin.Infrastructure.Entities;

public class DoctorPatientAssignmentEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DoctorId { get; set; }
    public Guid PatientId { get; set; }
    public string PatientEmail { get; set; } = string.Empty;
    public Guid AssignedByDoctorId { get; set; }
    public string? Notes { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}
