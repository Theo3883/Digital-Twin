namespace DigitalTwin.Domain.Models;

/// <summary>
/// Represents a doctor-to-patient assignment.
/// A doctor can only see patients that are explicitly assigned to them.
/// </summary>
public class DoctorPatientAssignment
{
    public long Id { get; set; }
    public long DoctorId { get; set; }
    public long PatientId { get; set; }

    /// <summary>
    /// The patient's email used to create the assignment (lookup key).
    /// </summary>
    public string PatientEmail { get; set; } = string.Empty;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The doctor who created this assignment (may differ from DoctorId in admin scenarios).
    /// </summary>
    public long AssignedByDoctorId { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
