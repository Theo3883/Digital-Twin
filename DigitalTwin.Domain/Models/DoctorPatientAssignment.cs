namespace DigitalTwin.Domain.Models;

/// <summary>
/// Represents a doctor-to-patient assignment.
/// A doctor can only see patients that are explicitly assigned to them.
/// </summary>
public class DoctorPatientAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DoctorId { get; set; }
    public Guid PatientId { get; set; }

    /// <summary>
    /// The patient's email used to create the assignment (lookup key).
    /// </summary>
    public string PatientEmail { get; set; } = string.Empty;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The doctor who created this assignment (may differ from DoctorId in admin scenarios).
    /// </summary>
    public Guid AssignedByDoctorId { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
