namespace DigitalTwin.Application.DTOs;

/// <summary>Full detail of a patient for the doctor portal.</summary>
public record DoctorPatientDetailDto
{
    public Guid PatientId { get; init; }
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public string? BloodType { get; init; }
    public string? Allergies { get; init; }
    public string? MedicalHistoryNotes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
