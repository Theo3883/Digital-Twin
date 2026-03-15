namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents a doctor assigned to a patient.
/// </summary>
public class AssignedDoctorDto
{
    /// <summary>
    /// Gets or sets the doctor identifier.
    /// </summary>
    public Guid DoctorId { get; set; }

    /// <summary>
    /// Gets or sets the doctor's display name.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the doctor's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the doctor's profile photo URL.
    /// </summary>
    public string? PhotoUrl { get; set; }

    /// <summary>
    /// Gets or sets when the doctor was assigned to the patient.
    /// </summary>
    public DateTime AssignedAt { get; set; }

    /// <summary>
    /// Gets or sets assignment notes.
    /// </summary>
    public string? Notes { get; set; }
}

