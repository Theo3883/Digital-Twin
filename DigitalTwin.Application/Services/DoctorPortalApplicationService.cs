using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Mappers;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Services;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Implements <see cref="IDoctorPortalApplicationService"/> using cloud-keyed repositories.
/// Every data access validates the doctor-patient assignment first.
/// </summary>
public class DoctorPortalApplicationService : IDoctorPortalApplicationService
{
    private readonly IDoctorPatientAssignmentRepository _assignments;
    private readonly IPatientRepository _patients;
    private readonly IUserRepository _users;
    private readonly IVitalSignRepository _vitals;
    private readonly ISleepSessionRepository _sleep;
    private readonly IMedicationRepository _medications;
    private readonly IMedicationService _medicationService;
    private readonly IRxCuiLookupProvider _rxCuiLookup;
    private readonly ILogger<DoctorPortalApplicationService> _logger;

    public DoctorPortalApplicationService(
        IDoctorPatientAssignmentRepository assignments,
        IPatientRepository patients,
        IUserRepository users,
        IVitalSignRepository vitals,
        ISleepSessionRepository sleep,
        IMedicationRepository medications,
        IMedicationService medicationService,
        IRxCuiLookupProvider rxCuiLookup,
        ILogger<DoctorPortalApplicationService> logger)
    {
        _assignments = assignments;
        _patients = patients;
        _users = users;
        _vitals = vitals;
        _sleep = sleep;
        _medications = medications;
        _medicationService = medicationService;
        _rxCuiLookup = rxCuiLookup;
        _logger = logger;
    }

    // ── Dashboard ────────────────────────────────────────────────────────────────

    public async Task<DoctorDashboardDto> GetDashboardAsync(string doctorEmail)
    {
        var doctor = await _users.GetByEmailAsync(doctorEmail);
        if (doctor is null)
            return new DoctorDashboardDto { DoctorEmail = doctorEmail };

        var assignments = (await _assignments.GetByDoctorIdAsync(doctor.Id)).ToList();

        return new DoctorDashboardDto
        {
            TotalAssignedPatients = assignments.Count,
            DoctorName = $"{doctor.FirstName} {doctor.LastName}".Trim(),
            DoctorEmail = doctor.Email
        };
    }

    // ── Patient list ─────────────────────────────────────────────────────────────

    public async Task<IEnumerable<DoctorPatientSummaryDto>> GetMyPatientsAsync(string doctorEmail)
    {
        var doctor = await _users.GetByEmailAsync(doctorEmail);
        if (doctor is null) return [];

        var assignments = await _assignments.GetByDoctorIdAsync(doctor.Id);
        var summaries = new List<DoctorPatientSummaryDto>();

        foreach (var a in assignments)
        {
            var patient = await _patients.GetByIdAsync(a.PatientId);
            if (patient is null) continue;

            var user = await _users.GetByIdAsync(patient.UserId);

            summaries.Add(new DoctorPatientSummaryDto
            {
                PatientId = patient.Id,
                Email = user?.Email ?? a.PatientEmail,
                FullName = user is not null ? $"{user.FirstName} {user.LastName}".Trim() : "Unknown",
                BloodType = patient.BloodType,
                AssignedAt = a.AssignedAt,
                PatientCreatedAt = patient.CreatedAt
            });
        }

        return summaries;
    }

    // ── Patient detail ───────────────────────────────────────────────────────────

    public async Task<DoctorPatientDetailDto?> GetPatientDetailAsync(string doctorEmail, Guid patientId)
    {
        if (!await IsAuthorizedForPatientAsync(doctorEmail, patientId))
            return null;

        var patient = await _patients.GetByIdAsync(patientId);
        if (patient is null) return null;

        var user = await _users.GetByIdAsync(patient.UserId);

        return new DoctorPatientDetailDto
        {
            PatientId = patient.Id,
            UserId = patient.UserId,
            Email = user?.Email ?? "unknown",
            FullName = user is not null ? $"{user.FirstName} {user.LastName}".Trim() : "Unknown",
            PhotoUrl = user?.PhotoUrl,
            BloodType = patient.BloodType,
            Allergies = patient.Allergies,
            MedicalHistoryNotes = patient.MedicalHistoryNotes,
            CreatedAt = patient.CreatedAt,
            UpdatedAt = patient.UpdatedAt
        };
    }

    // ── Vitals ───────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<VitalSignDto>> GetPatientVitalsAsync(
        string doctorEmail, Guid patientId,
        string? type = null, DateTime? from = null, DateTime? to = null)
    {
        if (!await IsAuthorizedForPatientAsync(doctorEmail, patientId))
            return [];

        Domain.Enums.VitalSignType? parsedType = null;
        if (type is not null && Enum.TryParse<Domain.Enums.VitalSignType>(type, true, out var t))
            parsedType = t;

        var vitals = await _vitals.GetByPatientAsync(patientId, parsedType, from, to);
        return vitals.Select(v => VitalSignMapper.ToDto(v));
    }

    // ── Sleep ────────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<SleepSessionDto>> GetPatientSleepAsync(
        string doctorEmail, Guid patientId,
        DateTime? from = null, DateTime? to = null)
    {
        if (!await IsAuthorizedForPatientAsync(doctorEmail, patientId))
            return [];

        var sessions = await _sleep.GetByPatientAsync(patientId, from, to);
        return sessions.Select(s => new SleepSessionDto
        {
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            DurationMinutes = s.DurationMinutes,
            QualityScore = s.QualityScore
        });
    }

    // ── Medications ──────────────────────────────────────────────────────────────

    public async Task<IEnumerable<MedicationDto>> GetPatientMedicationsAsync(string doctorEmail, Guid patientId)
    {
        if (!await IsAuthorizedForPatientAsync(doctorEmail, patientId))
            return [];

        var medications = await _medications.GetByPatientAsync(patientId);
        return medications.Select(MedicationToDto);
    }

    public async Task<MedicationDto?> AddPatientMedicationAsync(
        string doctorEmail, Guid patientId, AddMedicationDto dto)
    {
        if (!await IsAuthorizedForPatientAsync(doctorEmail, patientId))
            return null;

        var doctor = await _users.GetByEmailAsync(doctorEmail);

        var rxCui = dto.RxCui;
        if (string.IsNullOrWhiteSpace(rxCui) && !string.IsNullOrWhiteSpace(dto.Name))
            rxCui = await _rxCuiLookup.LookupRxCuiAsync(dto.Name.Trim());

        var medication = _medicationService.CreateMedication(
            patientId,
            dto.Name,
            dto.Dosage,
            dto.Frequency,
            dto.Route,
            rxCui,
            dto.Instructions,
            dto.Reason,
            prescribedByUserId: doctor?.Id,
            dto.StartDate,
            AddedByRole.Doctor);

        await _medications.AddAsync(medication);
        return MedicationToDto(medication);
    }

    public async Task<bool> DeletePatientMedicationAsync(
        string doctorEmail, Guid patientId, Guid medicationId)
    {
        if (!await IsAuthorizedForPatientAsync(doctorEmail, patientId))
            return false;

        var existing = await _medications.GetByIdAsync(medicationId);
        if (existing is null || existing.PatientId != patientId)
            return false;

        await _medications.SoftDeleteAsync(medicationId);
        return true;
    }

    public async Task<bool> DiscontinuePatientMedicationAsync(
        string doctorEmail, Guid patientId, Guid medicationId, string reason)
    {
        if (!await IsAuthorizedForPatientAsync(doctorEmail, patientId))
            return false;

        var existing = await _medications.GetByIdAsync(medicationId);
        if (existing is null || existing.PatientId != patientId)
            return false;

        await _medications.DiscontinueAsync(medicationId, DateTime.UtcNow, reason?.Trim());
        return true;
    }

    private static MedicationDto MedicationToDto(Domain.Models.Medication m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Dosage = m.Dosage,
        Frequency = m.Frequency,
        Route = m.Route,
        Status = m.Status,
        RxCui = m.RxCui,
        Instructions = m.Instructions,
        Reason = m.Reason,
        PrescribedByUserId = m.PrescribedByUserId,
        StartDate = m.StartDate,
        EndDate = m.EndDate,
        DiscontinuedReason = m.DiscontinuedReason,
        AddedByRole = m.AddedByRole,
        CreatedAt = m.CreatedAt
    };

    // ── Assign / Unassign ────────────────────────────────────────────────────────

    public async Task<DoctorPatientSummaryDto?> AssignPatientAsync(string doctorEmail, AssignPatientDto dto)
    {
        var doctor = await _users.GetByEmailAsync(doctorEmail);
        if (doctor is null)
        {
            _logger.LogWarning("[DoctorPortal] Doctor email {Email} not found.", doctorEmail);
            return null;
        }

        // Find the patient by email → User → Patient
        var patientUser = await _users.GetByEmailAsync(dto.PatientEmail);
        if (patientUser is null)
        {
            _logger.LogWarning("[DoctorPortal] Patient email {Email} not found.", dto.PatientEmail);
            return null;
        }

        var patient = await _patients.GetByUserIdAsync(patientUser.Id);
        if (patient is null)
        {
            _logger.LogWarning("[DoctorPortal] No patient profile for user {Email}.", dto.PatientEmail);
            return null;
        }

        // Check for duplicate
        if (await _assignments.IsAssignedAsync(doctor.Id, patient.Id))
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("[DoctorPortal] Patient {PatientId} already assigned to doctor {DoctorId}.",
                    patient.Id, doctor.Id);
            }
            return null;
        }

        var assignment = new DoctorPatientAssignment
        {
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            PatientEmail = dto.PatientEmail,
            AssignedByDoctorId = doctor.Id,
            Notes = dto.Notes
        };

        await _assignments.AddAsync(assignment);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[DoctorPortal] Patient {PatientId} assigned to doctor {DoctorId}.",
                patient.Id, doctor.Id);

        return new DoctorPatientSummaryDto
        {
            PatientId = patient.Id,
            Email = patientUser.Email,
            FullName = $"{patientUser.FirstName} {patientUser.LastName}".Trim(),
            BloodType = patient.BloodType,
            AssignedAt = assignment.AssignedAt,
            PatientCreatedAt = patient.CreatedAt
        };
    }

    public async Task<bool> UnassignPatientAsync(string doctorEmail, Guid patientId)
    {
        var doctor = await _users.GetByEmailAsync(doctorEmail);
        if (doctor is null) return false;

        if (!await _assignments.IsAssignedAsync(doctor.Id, patientId))
            return false;

        await _assignments.RemoveAsync(doctor.Id, patientId);
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[DoctorPortal] Patient {PatientId} unassigned from doctor {DoctorId}.",
                patientId, doctor.Id);
        return true;
    }

    // ── Authorization helper ─────────────────────────────────────────────────────

    private async Task<bool> IsAuthorizedForPatientAsync(string doctorEmail, Guid patientId)
    {
        var doctor = await _users.GetByEmailAsync(doctorEmail);
        if (doctor is null) return false;
        return await _assignments.IsAssignedAsync(doctor.Id, patientId);
    }
}
