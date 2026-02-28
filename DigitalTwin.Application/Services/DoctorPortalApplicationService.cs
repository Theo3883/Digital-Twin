using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Mappers;
using DigitalTwin.Domain.Interfaces.Repositories;
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
    private readonly ILogger<DoctorPortalApplicationService> _logger;

    public DoctorPortalApplicationService(
        IDoctorPatientAssignmentRepository assignments,
        IPatientRepository patients,
        IUserRepository users,
        IVitalSignRepository vitals,
        ISleepSessionRepository sleep,
        ILogger<DoctorPortalApplicationService> logger)
    {
        _assignments = assignments;
        _patients = patients;
        _users = users;
        _vitals = vitals;
        _sleep = sleep;
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

    public async Task<DoctorPatientDetailDto?> GetPatientDetailAsync(string doctorEmail, long patientId)
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
        string doctorEmail, long patientId,
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
        string doctorEmail, long patientId,
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
            _logger.LogInformation("[DoctorPortal] Patient {PatientId} already assigned to doctor {DoctorId}.",
                patient.Id, doctor.Id);
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

        _logger.LogInformation("[DoctorPortal] Patient {PatientId} assigned to doctor {DoctorId}.",
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

    public async Task<bool> UnassignPatientAsync(string doctorEmail, long patientId)
    {
        var doctor = await _users.GetByEmailAsync(doctorEmail);
        if (doctor is null) return false;

        if (!await _assignments.IsAssignedAsync(doctor.Id, patientId))
            return false;

        await _assignments.RemoveAsync(doctor.Id, patientId);
        _logger.LogInformation("[DoctorPortal] Patient {PatientId} unassigned from doctor {DoctorId}.",
            patientId, doctor.Id);
        return true;
    }

    // ── Authorization helper ─────────────────────────────────────────────────────

    private async Task<bool> IsAuthorizedForPatientAsync(string doctorEmail, long patientId)
    {
        var doctor = await _users.GetByEmailAsync(doctorEmail);
        if (doctor is null) return false;
        return await _assignments.IsAssignedAsync(doctor.Id, patientId);
    }
}
