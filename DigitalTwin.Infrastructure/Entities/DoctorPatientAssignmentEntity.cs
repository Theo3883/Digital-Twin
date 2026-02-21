namespace DigitalTwin.Infrastructure.Entities;

public class DoctorPatientAssignmentEntity
{
    public long Id { get; set; }
    public long DoctorId { get; set; }
    public long PatientId { get; set; }
    public string PatientEmail { get; set; } = string.Empty;
    public long AssignedByDoctorId { get; set; }
    public string? Notes { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}
