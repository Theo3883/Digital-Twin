namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents patient profile data prepared for UI display.
/// </summary>
public record PatientDisplayDto
{
    /// <summary>
    /// Gets the patient identifier.
    /// </summary>
    public Guid PatientId { get; init; }

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
}
