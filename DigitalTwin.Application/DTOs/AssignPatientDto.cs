namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents a request to assign a patient to a doctor by email.
/// </summary>
public record AssignPatientDto
{
    /// <summary>
    /// Gets the patient's email address.
    /// </summary>
    public string PatientEmail { get; init; } = string.Empty;

    /// <summary>
    /// Gets optional notes for the assignment.
    /// </summary>
    public string? Notes { get; init; }
}
