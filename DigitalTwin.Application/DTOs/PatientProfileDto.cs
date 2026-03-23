namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents editable patient medical profile fields.
/// </summary>
public record PatientProfileDto
{
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
}
