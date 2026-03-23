namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents the detailed patient view returned to the doctor portal.
/// </summary>
public record DoctorPatientDetailDto
{
    /// <summary>
    /// Gets the patient identifier.
    /// </summary>
    public Guid PatientId { get; init; }

    /// <summary>
    /// Gets the owning user identifier for the patient.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Gets the patient's email address.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Gets the patient's full name.
    /// </summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the patient's profile photo URL.
    /// </summary>
    public string? PhotoUrl { get; init; }

    /// <summary>
    /// Gets the patient's blood type.
    /// </summary>
    public string? BloodType { get; init; }

    /// <summary>
    /// Gets the patient's allergy information.
    /// </summary>
    public string? Allergies { get; init; }

    /// <summary>
    /// Gets the patient's medical history notes.
    /// </summary>
    public string? MedicalHistoryNotes { get; init; }

    /// <summary>
    /// Gets when the patient profile was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets when the patient profile was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}
