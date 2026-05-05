using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Defines doctor-portal operations that are scoped to assigned patients.
/// </summary>
public interface IDoctorPortalApplicationService
{
    /// <summary>
    /// Gets the dashboard summary for the specified doctor.
    /// </summary>
    Task<DoctorDashboardDto> GetDashboardAsync(string doctorEmail);

    /// <summary>
    /// Gets the patients assigned to the specified doctor.
    /// </summary>
    Task<IEnumerable<DoctorPatientSummaryDto>> GetMyPatientsAsync(string doctorEmail);

    /// <summary>
    /// Gets detailed information for an assigned patient.
    /// </summary>
    Task<DoctorPatientDetailDto?> GetPatientDetailAsync(string doctorEmail, Guid patientId);

    /// <summary>
    /// Gets vital-sign samples for an assigned patient.
    /// </summary>
    Task<IEnumerable<VitalSignDto>> GetPatientVitalsAsync(
        string doctorEmail, Guid patientId,
        string? type = null, DateTime? from = null, DateTime? to = null);

    /// <summary>
    /// Gets sleep sessions for an assigned patient.
    /// </summary>
    Task<IEnumerable<SleepSessionDto>> GetPatientSleepAsync(
        string doctorEmail, Guid patientId,
        DateTime? from = null, DateTime? to = null);

    /// <summary>
    /// Gets medications for an assigned patient.
    /// </summary>
    Task<IEnumerable<MedicationDto>> GetPatientMedicationsAsync(string doctorEmail, Guid patientId);

    /// <summary>
    /// Gets structured medical-history entries for an assigned patient.
    /// </summary>
    Task<IEnumerable<MedicalHistoryEntryDto>> GetPatientMedicalHistoryAsync(
        string doctorEmail, Guid patientId, int limit = 50);

    /// <summary>
    /// Gets medication interactions for the patient's ACTIVE medications.
    /// </summary>
    Task<IEnumerable<MedicationInteractionDto>> GetPatientMedicationInteractionsAsync(
        string doctorEmail, Guid patientId);

    /// <summary>
    /// Adds a doctor-prescribed medication for an assigned patient.
    /// </summary>
    Task<MedicationDto?> AddPatientMedicationAsync(string doctorEmail, Guid patientId, AddMedicationDto dto);

    /// <summary>
    /// Soft-deletes a medication for an assigned patient.
    /// </summary>
    Task<bool> DeletePatientMedicationAsync(string doctorEmail, Guid patientId, Guid medicationId);

    /// <summary>
    /// Discontinues a medication for an assigned patient.
    /// </summary>
    Task<bool> DiscontinuePatientMedicationAsync(string doctorEmail, Guid patientId, Guid medicationId, string reason);

    /// <summary>
    /// Assigns a patient to a doctor by patient email.
    /// </summary>
    Task<DoctorPatientSummaryDto?> AssignPatientAsync(string doctorEmail, AssignPatientDto dto);

    /// <summary>
    /// Removes a patient assignment from the doctor.
    /// </summary>
    Task<bool> UnassignPatientAsync(string doctorEmail, Guid patientId);
}
