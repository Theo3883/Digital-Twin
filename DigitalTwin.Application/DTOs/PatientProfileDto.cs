namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Data required to create or update a patient medical profile.
/// </summary>
public record PatientProfileDto
{
    public string? BloodType { get; init; }
    public string? Allergies { get; init; }
    public string? MedicalHistoryNotes { get; init; }
}
