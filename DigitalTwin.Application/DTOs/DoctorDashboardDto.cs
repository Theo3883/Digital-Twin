namespace DigitalTwin.Application.DTOs;

/// <summary>Dashboard summary for the doctor portal.</summary>
public record DoctorDashboardDto
{
    public int TotalAssignedPatients { get; init; }
    public string DoctorName { get; init; } = string.Empty;
    public string DoctorEmail { get; init; } = string.Empty;
}
