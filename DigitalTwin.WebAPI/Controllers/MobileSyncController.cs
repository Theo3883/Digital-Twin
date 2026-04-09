using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace DigitalTwin.WebAPI.Controllers;

/// <summary>
/// Mobile app sync endpoints — handles authentication and bidirectional data sync
/// between the iOS NativeAOT app and the cloud PostgreSQL database.
/// </summary>
[ApiController]
[Route("api/mobile")]
public class MobileSyncController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IUserRepository _userRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly IVitalSignRepository _vitalRepo;
    private readonly IMedicationRepository _medicationRepo;
    private readonly ISleepSessionRepository _sleepRepo;
    private readonly IEnvironmentReadingRepository _envRepo;
    private readonly IOcrDocumentRepository _ocrRepo;
    private readonly IMedicalHistoryEntryRepository _historyRepo;
    private readonly ILogger<MobileSyncController> _logger;

    public MobileSyncController(
        IConfiguration config,
        IUserRepository userRepo,
        IPatientRepository patientRepo,
        IVitalSignRepository vitalRepo,
        IMedicationRepository medicationRepo,
        ISleepSessionRepository sleepRepo,
        IEnvironmentReadingRepository envRepo,
        IOcrDocumentRepository ocrRepo,
        IMedicalHistoryEntryRepository historyRepo,
        ILogger<MobileSyncController> logger)
    {
        _config = config;
        _userRepo = userRepo;
        _patientRepo = patientRepo;
        _vitalRepo = vitalRepo;
        _medicationRepo = medicationRepo;
        _sleepRepo = sleepRepo;
        _envRepo = envRepo;
        _ocrRepo = ocrRepo;
        _historyRepo = historyRepo;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════════

    private string UserEmail =>
        User.FindFirstValue(ClaimTypes.Email)
        ?? throw new UnauthorizedAccessException("Email claim missing.");

    private async Task<(User user, Patient patient)> GetCurrentUserAndPatientAsync()
    {
        var user = await _userRepo.GetByEmailAsync(UserEmail)
                   ?? throw new UnauthorizedAccessException("User not found.");
        var patient = await _patientRepo.GetByUserIdAsync(user.Id)
                      ?? throw new InvalidOperationException("Patient profile not found.");
        return (user, patient);
    }

    private string GenerateJwt(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Authentication
    // ═══════════════════════════════════════════════════════════════════════════

    public record MobileAuthRequest(string GoogleIdToken);
    public record MobileAuthResponse(bool Success, string? AccessToken = null, string? ErrorMessage = null);

    /// <summary>POST /api/mobile/auth/google — authenticate mobile user via Google id_token.</summary>
    [HttpPost("auth/google")]
    public async Task<ActionResult<MobileAuthResponse>> GoogleAuth([FromBody] MobileAuthRequest request)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_config["Google:MobileClientId"] ?? _config["Google:ClientId"]!]
            };
            var payload = await GoogleJsonWebSignature.ValidateAsync(request.GoogleIdToken, settings);

            var user = await _userRepo.GetByEmailAsync(payload.Email);
            if (user is null)
            {
                // Auto-create patient user on first login
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = payload.Email,
                    FirstName = payload.GivenName,
                    LastName = payload.FamilyName,
                    Role = UserRole.Patient,
                };
                await _userRepo.AddAsync(user);

                var patient = new Patient
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                };
                await _patientRepo.AddAsync(patient);

                _logger.LogInformation("[MobileSync] Created new patient user: {Email}", payload.Email);
            }

            var jwt = GenerateJwt(user);
            return Ok(new MobileAuthResponse(true, jwt));
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "[MobileSync] Invalid Google token");
            return Unauthorized(new MobileAuthResponse(false, ErrorMessage: "Invalid Google token"));
        }
    }

    public record UserProfileDto(Guid Id, string Email, string? FirstName, string? LastName,
        string? PhotoUrl, string? Phone, DateTime? DateOfBirth);

    /// <summary>GET /api/mobile/auth/me — get current user profile.</summary>
    [Authorize]
    [HttpGet("auth/me")]
    public async Task<IActionResult> GetCurrentProfile()
    {
        var user = await _userRepo.GetByEmailAsync(UserEmail);
        if (user is null) return NotFound();
        return Ok(new { User = new UserProfileDto(user.Id, user.Email, user.FirstName, user.LastName,
            user.PhotoUrl, user.Phone, user.DateOfBirth) });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  User Sync
    // ═══════════════════════════════════════════════════════════════════════════

    public record SyncResult(bool Success, string? ErrorMessage = null);

    /// <summary>POST /api/mobile/sync/users/upsert — upsert user profile from device.</summary>
    [Authorize]
    [HttpPost("sync/users/upsert")]
    public async Task<ActionResult<SyncResult>> UpsertUser([FromBody] UpsertUserBody body)
    {
        var user = await _userRepo.GetByEmailAsync(UserEmail);
        if (user is null) return NotFound(new SyncResult(false, "User not found"));

        if (body.User is { } u)
        {
            user.FirstName = u.FirstName ?? user.FirstName;
            user.LastName = u.LastName ?? user.LastName;
            user.PhotoUrl = u.PhotoUrl ?? user.PhotoUrl;
            user.Phone = u.Phone ?? user.Phone;
            user.DateOfBirth = u.DateOfBirth ?? user.DateOfBirth;
            await _userRepo.UpdateAsync(user);
        }

        return Ok(new SyncResult(true));
    }

    public record UpsertUserBody(string? DeviceId, string? RequestId, UpsertUserData? User);
    public record UpsertUserData(string? Email, int? Role, string? FirstName, string? LastName,
        string? PhotoUrl, string? Phone, DateTime? DateOfBirth, string? Address, string? City, string? Country);

    // ═══════════════════════════════════════════════════════════════════════════
    //  Patient Sync
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>POST /api/mobile/sync/patients/upsert — upsert patient profile from device.</summary>
    [Authorize]
    [HttpPost("sync/patients/upsert")]
    public async Task<ActionResult<SyncResult>> UpsertPatient([FromBody] UpsertPatientBody body)
    {
        var user = await _userRepo.GetByEmailAsync(UserEmail);
        if (user is null) return NotFound(new SyncResult(false, "User not found"));

        var patient = await _patientRepo.GetByUserIdAsync(user.Id);
        if (patient is null)
        {
            patient = new Patient { Id = Guid.NewGuid(), UserId = user.Id };
            await _patientRepo.AddAsync(patient);
        }

        if (body.Patient is { } p)
        {
            patient.BloodType = p.BloodType ?? patient.BloodType;
            patient.Allergies = p.Allergies ?? patient.Allergies;
            patient.MedicalHistoryNotes = p.MedicalHistoryNotes ?? patient.MedicalHistoryNotes;
            patient.Weight = p.Weight ?? patient.Weight;
            patient.Height = p.Height ?? patient.Height;
            patient.BloodPressureSystolic = p.BloodPressureSystolic ?? patient.BloodPressureSystolic;
            patient.BloodPressureDiastolic = p.BloodPressureDiastolic ?? patient.BloodPressureDiastolic;
            patient.Cholesterol = p.Cholesterol ?? patient.Cholesterol;
            patient.Cnp = p.Cnp ?? patient.Cnp;
            await _patientRepo.UpdateAsync(patient);
        }

        return Ok(new SyncResult(true));
    }

    public record UpsertPatientBody(string? DeviceId, string? RequestId, UpsertPatientData? Patient);
    public record UpsertPatientData(string? BloodType, string? Allergies, string? MedicalHistoryNotes,
        decimal? Weight, decimal? Height, int? BloodPressureSystolic, int? BloodPressureDiastolic,
        decimal? Cholesterol, string? Cnp);

    /// <summary>GET /api/mobile/sync/patients/profile — pull patient profile to device.</summary>
    [Authorize]
    [HttpGet("sync/patients/profile")]
    public async Task<IActionResult> GetPatientProfile()
    {
        var user = await _userRepo.GetByEmailAsync(UserEmail);
        if (user is null) return NotFound();
        var patient = await _patientRepo.GetByUserIdAsync(user.Id);
        if (patient is null) return Ok(new { Patient = (object?)null });
        return Ok(new
        {
            Patient = new
            {
                patient.BloodType, patient.Allergies, patient.MedicalHistoryNotes,
                patient.Weight, patient.Height,
                patient.BloodPressureSystolic, patient.BloodPressureDiastolic,
                patient.Cholesterol, patient.Cnp
            }
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Vital Signs Sync
    // ═══════════════════════════════════════════════════════════════════════════

    public record VitalAppendBody(string? DeviceId, string? RequestId, List<VitalItem>? Items);
    public record VitalItem(int Type, double Value, string Unit, string Source, DateTime Timestamp);
    public record VitalSyncResult(int AcceptedCount, int DedupedCount);

    /// <summary>POST /api/mobile/sync/vitals/append — push vital signs from device.</summary>
    [Authorize]
    [HttpPost("sync/vitals/append")]
    public async Task<ActionResult<VitalSyncResult>> AppendVitals([FromBody] VitalAppendBody body)
    {
        var (_, patient) = await GetCurrentUserAndPatientAsync();

        if (body.Items is null or { Count: 0 })
            return Ok(new VitalSyncResult(0, 0));

        var accepted = 0;
        var deduped = 0;
        foreach (var item in body.Items)
        {
            if (await _vitalRepo.ExistsAsync(patient.Id, (VitalSignType)item.Type, item.Timestamp))
            {
                deduped++;
                continue;
            }

            await _vitalRepo.AddAsync(new VitalSign
            {
                PatientId = patient.Id,
                Type = (VitalSignType)item.Type,
                Value = item.Value,
                Unit = item.Unit,
                Source = item.Source,
                Timestamp = item.Timestamp
            });
            accepted++;
        }

        _logger.LogInformation("[MobileSync] Vitals: accepted={Accepted}, deduped={Deduped}", accepted, deduped);
        return Ok(new VitalSyncResult(accepted, deduped));
    }

    /// <summary>GET /api/mobile/sync/vitals — pull vital signs to device.</summary>
    [Authorize]
    [HttpGet("sync/vitals")]
    public async Task<IActionResult> GetVitals([FromQuery] DateTime fromUtc, [FromQuery] DateTime? toUtc)
    {
        var (_, patient) = await GetCurrentUserAndPatientAsync();
        var vitals = await _vitalRepo.GetByPatientAsync(patient.Id, null, fromUtc, toUtc);
        var items = vitals.Select(v => new VitalItem((int)v.Type, v.Value, v.Unit, v.Source, v.Timestamp));
        return Ok(new { Items = items });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Medications Sync
    // ═══════════════════════════════════════════════════════════════════════════

    public record MedicationSyncItem(
        Guid Id, string Name, string Dosage, string? Frequency, int Route,
        string? RxCui, string? Instructions, string? Reason,
        DateTime? StartDate, DateTime? EndDate, int Status,
        string? DiscontinuedReason, int AddedByRole, DateTime CreatedAt, DateTime UpdatedAt);

    /// <summary>POST /api/mobile/sync/medications/upsert — push medication changes from device.</summary>
    [Authorize]
    [HttpPost("sync/medications/upsert")]
    public async Task<ActionResult<SyncResult>> UpsertMedications([FromBody] MedicationUpsertBody body)
    {
        var (_, patient) = await GetCurrentUserAndPatientAsync();

        if (body.Items is null or { Count: 0 })
            return Ok(new SyncResult(true));

        var medications = body.Items.Select(m => new Medication
        {
            Id = m.Id,
            PatientId = patient.Id,
            Name = m.Name,
            Dosage = m.Dosage,
            Frequency = m.Frequency,
            Route = (MedicationRoute)m.Route,
            RxCui = m.RxCui,
            Instructions = m.Instructions,
            Reason = m.Reason,
            StartDate = m.StartDate,
            EndDate = m.EndDate,
            Status = (MedicationStatus)m.Status,
            DiscontinuedReason = m.DiscontinuedReason,
            AddedByRole = (AddedByRole)m.AddedByRole,
            CreatedAt = m.CreatedAt,
            UpdatedAt = m.UpdatedAt
        }).ToList();

        await _medicationRepo.UpsertRangeAsync(medications);
        _logger.LogInformation("[MobileSync] Upserted {Count} medications", medications.Count);
        return Ok(new SyncResult(true));
    }

    public record MedicationUpsertBody(string? DeviceId, string? RequestId, List<MedicationSyncItem>? Items);

    /// <summary>GET /api/mobile/sync/medications — pull medications for patient.</summary>
    [Authorize]
    [HttpGet("sync/medications")]
    public async Task<IActionResult> GetMedications()
    {
        var (_, patient) = await GetCurrentUserAndPatientAsync();
        var meds = await _medicationRepo.GetByPatientAsync(patient.Id);
        var items = meds.Select(m => new MedicationSyncItem(
            m.Id, m.Name, m.Dosage, m.Frequency, (int)m.Route,
            m.RxCui, m.Instructions, m.Reason,
            m.StartDate, m.EndDate, (int)m.Status,
            m.DiscontinuedReason, (int)m.AddedByRole, m.CreatedAt, m.UpdatedAt));
        return Ok(new { Items = items });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Environment Readings Sync
    // ═══════════════════════════════════════════════════════════════════════════

    public record EnvironmentAppendBody(string? DeviceId, string? RequestId, List<EnvironmentSyncItem>? Items);
    public record EnvironmentSyncItem(
        double Latitude, double Longitude, string LocationDisplayName,
        double PM25, double PM10, double O3, double NO2,
        double Temperature, double Humidity, int AirQuality, int AqiIndex,
        DateTime Timestamp);

    /// <summary>POST /api/mobile/sync/environment/append — push environment readings.</summary>
    [Authorize]
    [HttpPost("sync/environment/append")]
    public async Task<ActionResult<SyncResult>> AppendEnvironment([FromBody] EnvironmentAppendBody body)
    {
        if (body.Items is null or { Count: 0 })
            return Ok(new SyncResult(true));

        var readings = body.Items.Select(e => new EnvironmentReading
        {
            Latitude = e.Latitude,
            Longitude = e.Longitude,
            LocationDisplayName = e.LocationDisplayName,
            PM25 = e.PM25,
            PM10 = e.PM10,
            O3 = e.O3,
            NO2 = e.NO2,
            Temperature = e.Temperature,
            Humidity = e.Humidity,
            AirQuality = (AirQualityLevel)e.AirQuality,
            AqiIndex = e.AqiIndex,
            Timestamp = e.Timestamp
        }).ToList();

        await _envRepo.AddRangeAsync(readings, markDirty: false);
        _logger.LogInformation("[MobileSync] Appended {Count} environment readings", readings.Count);
        return Ok(new SyncResult(true));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Sleep Sessions Sync
    // ═══════════════════════════════════════════════════════════════════════════

    public record SleepAppendBody(string? DeviceId, string? RequestId, List<SleepSyncItem>? Items);
    public record SleepSyncItem(DateTime StartTime, DateTime EndTime, int DurationMinutes, double QualityScore);

    /// <summary>POST /api/mobile/sync/sleep/append — push sleep sessions.</summary>
    [Authorize]
    [HttpPost("sync/sleep/append")]
    public async Task<ActionResult<SyncResult>> AppendSleep([FromBody] SleepAppendBody body)
    {
        var (_, patient) = await GetCurrentUserAndPatientAsync();

        if (body.Items is null or { Count: 0 })
            return Ok(new SyncResult(true));

        var sessions = body.Items.Select(s => new SleepSession
        {
            PatientId = patient.Id,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            DurationMinutes = s.DurationMinutes,
            QualityScore = s.QualityScore
        }).ToList();

        await _sleepRepo.AddRangeAsync(sessions, markDirty: false);
        _logger.LogInformation("[MobileSync] Appended {Count} sleep sessions", sessions.Count);
        return Ok(new SyncResult(true));
    }

    /// <summary>GET /api/mobile/sync/sleep — pull sleep sessions.</summary>
    [Authorize]
    [HttpGet("sync/sleep")]
    public async Task<IActionResult> GetSleep([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        var (_, patient) = await GetCurrentUserAndPatientAsync();
        var sessions = await _sleepRepo.GetByPatientAsync(patient.Id, fromUtc, toUtc);
        var items = sessions.Select(s => new SleepSyncItem(s.StartTime, s.EndTime, s.DurationMinutes, s.QualityScore));
        return Ok(new { Items = items });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  OCR Documents Sync
    // ═══════════════════════════════════════════════════════════════════════════

    public record OcrDocumentSyncItem(
        Guid Id, string OpaqueInternalName, string MimeType, int PageCount,
        string SanitizedOcrPreview, DateTime ScannedAt);

    /// <summary>POST /api/mobile/sync/ocr-documents/upsert — push OCR document metadata.</summary>
    [Authorize]
    [HttpPost("sync/ocr-documents/upsert")]
    public async Task<ActionResult<SyncResult>> UpsertOcrDocuments([FromBody] OcrDocumentUpsertBody body)
    {
        var (_, patient) = await GetCurrentUserAndPatientAsync();

        if (body.Items is null or { Count: 0 })
            return Ok(new SyncResult(true));

        var documents = body.Items.Select(d => new OcrDocument
        {
            Id = d.Id,
            PatientId = patient.Id,
            OpaqueInternalName = d.OpaqueInternalName,
            MimeType = d.MimeType,
            PageCount = d.PageCount,
            SanitizedOcrPreview = d.SanitizedOcrPreview,
            ScannedAt = d.ScannedAt,
            IsDirty = false
        }).ToList();

        await _ocrRepo.UpsertRangeAsync(documents);
        _logger.LogInformation("[MobileSync] Upserted {Count} OCR documents", documents.Count);
        return Ok(new SyncResult(true));
    }

    public record OcrDocumentUpsertBody(string? DeviceId, string? RequestId, List<OcrDocumentSyncItem>? Items);

    // ═══════════════════════════════════════════════════════════════════════════
    //  Medical History Sync
    // ═══════════════════════════════════════════════════════════════════════════

    public record MedicalHistorySyncItem(
        Guid Id, Guid SourceDocumentId, string Title, string MedicationName,
        string Dosage, string Frequency, string Duration, string Notes,
        string Summary, decimal Confidence, DateTime EventDate);

    /// <summary>POST /api/mobile/sync/medical-history/append — push medical history entries.</summary>
    [Authorize]
    [HttpPost("sync/medical-history/append")]
    public async Task<ActionResult<SyncResult>> AppendMedicalHistory([FromBody] MedicalHistoryAppendBody body)
    {
        var (_, patient) = await GetCurrentUserAndPatientAsync();

        if (body.Items is null or { Count: 0 })
            return Ok(new SyncResult(true));

        var entries = body.Items.Select(e => new MedicalHistoryEntry
        {
            Id = e.Id,
            PatientId = patient.Id,
            SourceDocumentId = e.SourceDocumentId,
            Title = e.Title,
            MedicationName = e.MedicationName,
            Dosage = e.Dosage,
            Frequency = e.Frequency,
            Duration = e.Duration,
            Notes = e.Notes,
            Summary = e.Summary,
            Confidence = e.Confidence,
            EventDate = e.EventDate,
            IsDirty = false
        }).ToList();

        await _historyRepo.UpsertRangeAsync(entries);
        _logger.LogInformation("[MobileSync] Appended {Count} medical history entries", entries.Count);
        return Ok(new SyncResult(true));
    }

    public record MedicalHistoryAppendBody(string? DeviceId, string? RequestId, List<MedicalHistorySyncItem>? Items);
}
