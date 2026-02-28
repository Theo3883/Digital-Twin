using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Application service for the Doctor Portal.
/// All methods enforce doctor-patient assignment scoping — a doctor
/// can only access patients explicitly assigned to them.
/// </summary>
public interface IDoctorPortalApplicationService
{
    /// <summary>Get dashboard summary for a doctor.</summary>
    Task<DoctorDashboardDto> GetDashboardAsync(string doctorEmail);

    /// <summary>Get all patients assigned to a doctor.</summary>
    Task<IEnumerable<DoctorPatientSummaryDto>> GetMyPatientsAsync(string doctorEmail);

    /// <summary>Get full detail of a patient (only if assigned to this doctor).</summary>
    Task<DoctorPatientDetailDto?> GetPatientDetailAsync(string doctorEmail, Guid patientId);

    /// <summary>Get vital signs for an assigned patient.</summary>
    Task<IEnumerable<VitalSignDto>> GetPatientVitalsAsync(
        string doctorEmail, Guid patientId,
        string? type = null, DateTime? from = null, DateTime? to = null);

    /// <summary>Get sleep sessions for an assigned patient.</summary>
    Task<IEnumerable<SleepSessionDto>> GetPatientSleepAsync(
        string doctorEmail, Guid patientId,
        DateTime? from = null, DateTime? to = null);

    /// <summary>Assign a patient to a doctor by patient email.</summary>
    Task<DoctorPatientSummaryDto?> AssignPatientAsync(string doctorEmail, AssignPatientDto dto);

    /// <summary>Remove a patient assignment.</summary>
    Task<bool> UnassignPatientAsync(string doctorEmail, Guid patientId);
}
