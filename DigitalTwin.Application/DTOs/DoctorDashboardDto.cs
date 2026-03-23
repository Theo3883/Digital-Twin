namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents the dashboard summary shown in the doctor portal.
/// </summary>
public record DoctorDashboardDto
{
    /// <summary>
    /// Gets the total number of patients assigned to the doctor.
    /// </summary>
    public int TotalAssignedPatients { get; init; }

    /// <summary>
    /// Gets the doctor's display name.
    /// </summary>
    public string DoctorName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the doctor's email address.
    /// </summary>
    public string DoctorEmail { get; init; } = string.Empty;
}
