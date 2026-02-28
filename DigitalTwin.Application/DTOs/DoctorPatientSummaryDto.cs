namespace DigitalTwin.Application.DTOs;

/// <summary>Summary of a patient for the doctor's patient list.</summary>
public record DoctorPatientSummaryDto
{
    public long PatientId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? BloodType { get; init; }
    public DateTime AssignedAt { get; init; }
    public DateTime PatientCreatedAt { get; init; }
}
