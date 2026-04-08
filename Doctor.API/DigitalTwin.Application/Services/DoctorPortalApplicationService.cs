using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Mappers;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Exceptions;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Interfaces.Services;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using DigitalTwin.Domain.Services;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Orchestrates doctor-portal workflows by delegating authorization and core rules to the domain layer.
/// </summary>
public class DoctorPortalApplicationService : IDoctorPortalApplicationService
{
    private readonly IDoctorPortalDomainService              _domain;
    private readonly IRxCuiLookupProvider                    _rxCuiLookup;
    private readonly IVitalSignService                       _vitalSignService;
    private readonly IMedicalHistoryEntryRepository          _history;
    private readonly IMedicationInteractionProvider          _interactionsProvider;
    private readonly AppDebugLogger<DoctorPortalApplicationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DoctorPortalApplicationService"/> class.
    /// </summary>
    public DoctorPortalApplicationService(
        IDoctorPortalDomainService domain,
        IRxCuiLookupProvider rxCuiLookup,
        IVitalSignService vitalSignService,
        IMedicalHistoryEntryRepository history,
        IMedicationInteractionProvider interactionsProvider,
        AppDebugLogger<DoctorPortalApplicationService> logger)
    {
        _domain            = domain;
        _rxCuiLookup       = rxCuiLookup;
        _vitalSignService  = vitalSignService;
        _history           = history;
        _interactionsProvider = interactionsProvider;
        _logger            = logger;
    }

    /// <summary>
    /// Gets the dashboard summary for the specified doctor.
    /// </summary>
    public async Task<DoctorDashboardDto> GetDashboardAsync(string doctorEmail)
    {
        User doctor;
        try { doctor = await _domain.GetDoctorByEmailAsync(doctorEmail); }
        catch (NotFoundException) { return new DoctorDashboardDto { DoctorEmail = doctorEmail }; }

        var assignments = (await _domain.GetAssignmentsForDoctorAsync(doctor.Id)).ToList();

        return new DoctorDashboardDto
        {
            TotalAssignedPatients = assignments.Count,
            DoctorName            = doctor.FullName,
            DoctorEmail           = doctor.Email
        };
    }

    /// <summary>
    /// Gets the patients assigned to the specified doctor.
    /// </summary>
    public async Task<IEnumerable<DoctorPatientSummaryDto>> GetMyPatientsAsync(string doctorEmail)
    {
        User doctor;
        try { doctor = await _domain.GetDoctorByEmailAsync(doctorEmail); }
        catch (NotFoundException) { return []; }

        var assignments = await _domain.GetAssignmentsForDoctorAsync(doctor.Id);
        var summaries   = new List<DoctorPatientSummaryDto>();

        foreach (var a in assignments)
        {
            try
            {
                var (user, patient) = await _domain.GetPatientWithUserAsync(a.PatientId);
                summaries.Add(new DoctorPatientSummaryDto
                {
                    PatientId        = patient.Id,
                    Email            = user.Email,
                    FullName         = user.FullName,
                    BloodType        = patient.BloodType,
                    AssignedAt       = a.AssignedAt,
                    PatientCreatedAt = patient.CreatedAt
                });
            }
            catch (NotFoundException) { /* skip orphaned assignment */ }
        }

        return summaries;
    }

    /// <summary>
    /// Gets detailed information for an assigned patient.
    /// </summary>
    public async Task<DoctorPatientDetailDto?> GetPatientDetailAsync(string doctorEmail, Guid patientId)
    {
        try { await _domain.RequireAuthorizedDoctorIdAsync(doctorEmail, patientId); }
        catch (UnauthorizedException) { return null; }

        try
        {
            var (user, patient) = await _domain.GetPatientWithUserAsync(patientId);
            return new DoctorPatientDetailDto
            {
                PatientId           = patient.Id,
                UserId              = patient.UserId,
                Email               = user.Email,
                FullName            = user.FullName,
                PhotoUrl            = user.PhotoUrl,
                BloodType           = patient.BloodType,
                Allergies           = patient.Allergies,
                MedicalHistoryNotes = patient.MedicalHistoryNotes,
                Weight              = patient.Weight,
                Height              = patient.Height,
                BloodPressureSystolic  = patient.BloodPressureSystolic,
                BloodPressureDiastolic = patient.BloodPressureDiastolic,
                Cholesterol         = patient.Cholesterol,
                Cnp                 = patient.Cnp,
                CreatedAt           = patient.CreatedAt,
                UpdatedAt           = patient.UpdatedAt
            };
        }
        catch (NotFoundException) { return null; }
    }

    /// <summary>
    /// Gets vital-sign samples for an assigned patient.
    /// </summary>
    public async Task<IEnumerable<VitalSignDto>> GetPatientVitalsAsync(
        string doctorEmail, Guid patientId,
        string? type = null, DateTime? from = null, DateTime? to = null)
    {
        try { await _domain.RequireAuthorizedDoctorIdAsync(doctorEmail, patientId); }
        catch (UnauthorizedException) { return []; }

        VitalSignType? parsedType = _vitalSignService.TryParseType(type, out var t) ? t : null;

        var vitals = await _domain.GetPatientVitalsAsync(patientId, parsedType, from, to);
        return vitals.Select(v => VitalSignMapper.ToDto(v));
    }

    /// <summary>
    /// Gets sleep sessions for an assigned patient.
    /// </summary>
    public async Task<IEnumerable<SleepSessionDto>> GetPatientSleepAsync(
        string doctorEmail, Guid patientId,
        DateTime? from = null, DateTime? to = null)
    {
        try { await _domain.RequireAuthorizedDoctorIdAsync(doctorEmail, patientId); }
        catch (UnauthorizedException) { return []; }

        var sessions = await _domain.GetPatientSleepAsync(patientId, from, to);
        return sessions.Select(s => new SleepSessionDto
        {
            StartTime       = s.StartTime,
            EndTime         = s.EndTime,
            DurationMinutes = s.DurationMinutes,
            QualityScore    = s.QualityScore
        });
    }

    /// <summary>
    /// Gets medications for an assigned patient.
    /// </summary>
    public async Task<IEnumerable<MedicationDto>> GetPatientMedicationsAsync(
        string doctorEmail, Guid patientId)
    {
        try { await _domain.RequireAuthorizedDoctorIdAsync(doctorEmail, patientId); }
        catch (UnauthorizedException) { return []; }

        var medications = await _domain.GetPatientMedicationsAsync(patientId);
        return medications.Select(MedicationToDto);
    }

    public async Task<IEnumerable<MedicalHistoryEntryDto>> GetPatientMedicalHistoryAsync(
        string doctorEmail, Guid patientId, int limit = 50)
    {
        try { await _domain.RequireAuthorizedDoctorIdAsync(doctorEmail, patientId); }
        catch (UnauthorizedException) { return []; }

        // Structured OCR history entries (cloud-only in WebAPI host).
        var entries = (await _history.GetByPatientAsync(patientId))
            .Where(x => x.DeletedAt is null)
            .OrderByDescending(x => x.EventDate)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(x => new MedicalHistoryEntryDto
            {
                Id = x.Id,
                PatientId = x.PatientId,
                SourceDocumentId = x.SourceDocumentId,
                Title = x.Title,
                MedicationName = x.MedicationName,
                Dosage = x.Dosage,
                Frequency = x.Frequency,
                Duration = x.Duration,
                Notes = x.Notes,
                Summary = x.Summary,
                Confidence = x.Confidence,
                EventDate = x.EventDate,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            });

        return entries;
    }

    public async Task<IEnumerable<MedicationInteractionDto>> GetPatientMedicationInteractionsAsync(
        string doctorEmail, Guid patientId)
    {
        try { await _domain.RequireAuthorizedDoctorIdAsync(doctorEmail, patientId); }
        catch (UnauthorizedException) { return []; }

        var meds = await _domain.GetPatientMedicationsAsync(patientId);
        var rxCuis = meds
            .Where(m => m.Status == MedicationStatus.Active)
            .Select(m => m.RxCui)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rxCuis.Count < 2)
            return [];

        var interactions = await _interactionsProvider.GetInteractionsAsync(rxCuis);
        return interactions.Select(MedicationInteractionMapper.ToDto);
    }

    /// <summary>
    /// Adds a doctor-prescribed medication for an assigned patient.
    /// </summary>
    public async Task<MedicationDto?> AddPatientMedicationAsync(
        string doctorEmail, Guid patientId, AddMedicationDto dto)
    {
        Guid doctorId;
        try { doctorId = await _domain.RequireAuthorizedDoctorIdAsync(doctorEmail, patientId); }
        catch (UnauthorizedException) { return null; }

        // Resolve RxCUI when not provided by the caller (infrastructure concern handled here,
        // keeping the domain service free of external provider dependencies).
        var rxCui = dto.RxCui;
        if (string.IsNullOrWhiteSpace(rxCui) && !string.IsNullOrWhiteSpace(dto.Name))
            rxCui = await _rxCuiLookup.LookupRxCuiAsync(dto.Name.Trim());

        var request = new CreateMedicationRequest(
            PatientId:          patientId,
            Name:               dto.Name,
            Dosage:             dto.Dosage,
            Frequency:          dto.Frequency,
            Route:              dto.Route,
            RxCui:              rxCui,
            Instructions:       dto.Instructions,
            Reason:             dto.Reason,
            PrescribedByUserId: doctorId,
            StartDate:          dto.StartDate,
            AddedByRole:        AddedByRole.Doctor);

        var medication = await _domain.AddPatientMedicationAsync(patientId, request);
        return MedicationToDto(medication);
    }

    /// <summary>
    /// Soft-deletes a medication for an assigned patient.
    /// </summary>
    public async Task<bool> DeletePatientMedicationAsync(
        string doctorEmail, Guid patientId, Guid medicationId)
    {
        try { await _domain.RequireAuthorizedDoctorIdAsync(doctorEmail, patientId); }
        catch (UnauthorizedException) { return false; }

        try
        {
            await _domain.SoftDeleteMedicationAsync(patientId, medicationId);
            return true;
        }
        catch (NotFoundException) { return false; }
        catch (MedicationOwnershipException) { return false; }
    }

    /// <summary>
    /// Discontinues a medication for an assigned patient.
    /// </summary>
    public async Task<bool> DiscontinuePatientMedicationAsync(
        string doctorEmail, Guid patientId, Guid medicationId, string reason)
    {
        try { await _domain.RequireAuthorizedDoctorIdAsync(doctorEmail, patientId); }
        catch (UnauthorizedException) { return false; }

        try
        {
            await _domain.DiscontinueMedicationAsync(patientId, medicationId, reason);
            return true;
        }
        catch (NotFoundException) { return false; }
        catch (MedicationOwnershipException) { return false; }
    }

    /// <summary>
    /// Assigns a patient to the doctor and returns the resulting patient summary.
    /// </summary>
    public async Task<DoctorPatientSummaryDto?> AssignPatientAsync(
        string doctorEmail, AssignPatientDto dto)
    {
        try
        {
            var assignment = await _domain.AssignPatientAsync(
                doctorEmail, dto.PatientEmail, dto.Notes);

            var (user, patient) = await _domain.GetPatientWithUserAsync(assignment.PatientId);

            return new DoctorPatientSummaryDto
            {
                PatientId        = patient.Id,
                Email            = user.Email,
                FullName         = user.FullName,
                BloodType        = patient.BloodType,
                AssignedAt       = assignment.AssignedAt,
                PatientCreatedAt = patient.CreatedAt
            };
        }
        catch (NotFoundException ex)
        {
            _logger.Warn(ex, "[DoctorPortal] Assignment failed: {Message}", ex.Message);
            return null;
        }
        catch (DuplicateAssignmentException ex)
        {
            _logger.Info("[DoctorPortal] {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Removes a patient assignment from the specified doctor.
    /// </summary>
    public async Task<bool> UnassignPatientAsync(string doctorEmail, Guid patientId)
    {
        try
        {
            await _domain.UnassignPatientAsync(doctorEmail, patientId);
            return true;
        }
        catch (NotFoundException)
        {
            return false;
        }
    }

    private static MedicationDto MedicationToDto(Medication m) => new()
    {
        Id                 = m.Id,
        Name               = m.Name,
        Dosage             = m.Dosage,
        Frequency          = m.Frequency,
        Route              = m.Route,
        Status             = m.Status,
        RxCui              = m.RxCui,
        Instructions       = m.Instructions,
        Reason             = m.Reason,
        PrescribedByUserId = m.PrescribedByUserId,
        StartDate          = m.StartDate,
        EndDate            = m.EndDate,
        DiscontinuedReason = m.DiscontinuedReason,
        AddedByRole        = m.AddedByRole,
        CreatedAt          = m.CreatedAt
    };
}
