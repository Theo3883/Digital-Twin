namespace DigitalTwin.Application.DTOs;

/// <summary>Request to assign a patient to a doctor by email.</summary>
public record AssignPatientDto
{
    public string PatientEmail { get; init; } = string.Empty;
    public string? Notes { get; init; }
}
