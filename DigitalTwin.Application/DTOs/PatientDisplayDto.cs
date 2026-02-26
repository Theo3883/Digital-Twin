namespace DigitalTwin.Application.DTOs;

/// <summary>Read-only patient profile data for display in the UI.</summary>
public record PatientDisplayDto
{
    public long PatientId { get; init; }
    public string? BloodType { get; init; }
    public string? Allergies { get; init; }
    public string? MedicalHistoryNotes { get; init; }
    public DateTime CreatedAt { get; init; }
}
