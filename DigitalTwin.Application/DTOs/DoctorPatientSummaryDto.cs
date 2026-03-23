namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents a patient summary for the doctor's patient list.
/// </summary>
public record DoctorPatientSummaryDto
{
    /// <summary>
    /// Gets the patient identifier.
    /// </summary>
    public Guid PatientId { get; init; }

    /// <summary>
    /// Gets the patient's email address.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Gets the patient's full name.
    /// </summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the patient's blood type.
    /// </summary>
    public string? BloodType { get; init; }

    /// <summary>
    /// Gets when the patient was assigned to the doctor.
    /// </summary>
    public DateTime AssignedAt { get; init; }

    /// <summary>
    /// Gets when the patient profile was created.
    /// </summary>
    public DateTime PatientCreatedAt { get; init; }
}
