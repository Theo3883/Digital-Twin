using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using DigitalTwin.Application.Interfaces;
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
    private readonly IDoctorAssignmentApplicationService _doctorAssignmentService;
    private readonly IDoctorPatientAssignmentRepository _assignments;
    private readonly INotificationRepository _notifications;
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
        IDoctorAssignmentApplicationService doctorAssignmentService,
        IDoctorPatientAssignmentRepository assignments,
        INotificationRepository notifications,
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
        _doctorAssignmentService = doctorAssignmentService;
        _assignments = assignments;
        _notifications = notifications;
        _logger = logger;
    }

    public record CriticalAlertSyncItem(string RuleName, string Message, DateTime Timestamp);
    public record DeviceRequestEnvelope<T>(string? DeviceId, string? RequestId, T? User, T? Patient, List<T>? Items);

    // ═══════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════════

    private string UserEmail =>
        User.FindFirstValue(ClaimTypes.Email)
        ?? throw new UnauthorizedAccessException("Email claim missing.");

    private string[] GetGoogleAudiences()
    {
        // iOS and web can use different Google OAuth client IDs.
        // Accept multiple audiences to support:
        // - iOS (Google:MobileClientId or Google:MobileClientIds)
        // - existing web/doctor portal (Google:ClientId)
        var mobileSingle = _config["Google:MobileClientId"];
        var mobileMany = _config["Google:MobileClientIds"];
        var defaultClient = _config["Google:ClientId"];

        var values = new List<string>();

        static IEnumerable<string> SplitCsv(string? s) =>
            string.IsNullOrWhiteSpace(s)
                ? []
                : s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        values.AddRange(SplitCsv(mobileMany));
        if (!string.IsNullOrWhiteSpace(mobileSingle)) values.Add(mobileSingle);
        if (!string.IsNullOrWhiteSpace(defaultClient)) values.Add(defaultClient);

        // De-dup; empty means misconfig (we'll throw a clear error later).
        return values.Distinct(StringComparer.Ordinal).ToArray();
    }

    private async Task<(User user, Patient patient)> GetCurrentUserAndPatientAsync()
    {
        var user = await _userRepo.GetByEmailAsync(UserEmail)
                   ?? throw new UnauthorizedAccessException("User not found.");
        var patient = await _patientRepo.GetByUserIdAsync(user.Id)
                      ?? throw new InvalidOperationException("Patient profile not found.");
        return (user, patient);
    }

    public record BootstrapPatientDto(
        Guid Id,
        Guid UserId,
        string? BloodType,
        string? Allergies,
        string? MedicalHistoryNotes,
        decimal? Weight,
        decimal? Height,
        int? BloodPressureSystolic,
        int? BloodPressureDiastolic,
        decimal? Cholesterol,
        string? Cnp);

    public record BootstrapVitalDto(
        Guid Id,
        int Type,
        double Value,
        string Unit,
        string Source,
        DateTime Timestamp);

    public record BootstrapSleepDto(
        Guid Id,
        DateTime StartTime,
        DateTime EndTime,
        int DurationMinutes,
        double QualityScore);

    public record BootstrapEnvironmentDto(
        Guid Id,
        double Latitude,
        double Longitude,
        string LocationDisplayName,
        double PM25,
        double PM10,
        double O3,
        double NO2,
        double Temperature,
        double Humidity,
        int AirQuality,
        int AqiIndex,
        DateTime Timestamp);

    public record MobileBootstrap(
        UserProfileDto User,
        BootstrapPatientDto? Patient,
        IEnumerable<BootstrapVitalDto> Vitals,
        IEnumerable<MedicationSyncItem> Medications,
        IEnumerable<BootstrapSleepDto> SleepSessions,
        IEnumerable<BootstrapEnvironmentDto> EnvironmentReadings,
        IEnumerable<OcrDocumentSyncItem> OcrDocuments,
        IEnumerable<MedicalHistorySyncItem> MedicalHistoryEntries);

    private static Guid StableGuid(params string[] parts)
    {
        var joined = string.Join("|", parts);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }

    private async Task<MobileBootstrap> BuildBootstrapAsync(User user)
    {
        var userDto = new UserProfileDto(user.Id, user.Email, (int)user.Role, user.FirstName, user.LastName, user.PhotoUrl, user.Phone, user.Address, user.City, user.Country, user.DateOfBirth);

        var patient = await _patientRepo.GetByUserIdAsync(user.Id);
        if (patient is null)
        {
            return new MobileBootstrap(
                userDto,
                Patient: null,
                Vitals: [],
                Medications: [],
                SleepSessions: [],
                EnvironmentReadings: [],
                OcrDocuments: [],
                MedicalHistoryEntries: []);
        }

        var vitals = await _vitalRepo.GetByPatientAsync(patient.Id, null, null, null);
        var vitalItems = vitals.Select(v => new BootstrapVitalDto(
            Id: StableGuid("vital", patient.Id.ToString(), ((int)v.Type).ToString(), v.Timestamp.ToUniversalTime().ToString("O")),
            Type: (int)v.Type,
            Value: v.Value,
            Unit: v.Unit,
            Source: v.Source,
            Timestamp: v.Timestamp));

        var meds = await _medicationRepo.GetByPatientAsync(patient.Id);
        var medItems = meds.Select(m => new MedicationSyncItem(
            m.Id, m.Name, m.Dosage, m.Frequency, (int)m.Route,
            m.RxCui, m.Instructions, m.Reason,
            m.StartDate, m.EndDate, (int)m.Status,
            m.DiscontinuedReason, (int)m.AddedByRole, m.CreatedAt, m.UpdatedAt));

        var sleep = await _sleepRepo.GetByPatientAsync(patient.Id, null, null);
        var sleepItems = sleep.Select(s => new BootstrapSleepDto(
            Id: StableGuid("sleep", patient.Id.ToString(), s.StartTime.ToUniversalTime().ToString("O")),
            StartTime: s.StartTime,
            EndTime: s.EndTime,
            DurationMinutes: s.DurationMinutes,
            QualityScore: s.QualityScore));

        // Environment readings are not tied to a patient in the current schema.
        // Pull a bounded window to avoid unbounded payloads.
        var since = DateTime.UtcNow.AddDays(-30);
        var env = await _envRepo.GetSinceAsync(since, limit: 1000);
        var envItems = env.Select(e => new BootstrapEnvironmentDto(
            Id: StableGuid("env", e.Timestamp.ToUniversalTime().ToString("O"), e.Latitude.ToString("R"), e.Longitude.ToString("R")),
            Latitude: e.Latitude,
            Longitude: e.Longitude,
            LocationDisplayName: e.LocationDisplayName,
            PM25: e.PM25,
            PM10: e.PM10,
            O3: e.O3,
            NO2: e.NO2,
            Temperature: e.Temperature,
            Humidity: e.Humidity,
            AirQuality: (int)e.AirQuality,
            AqiIndex: e.AqiIndex,
            Timestamp: e.Timestamp));

        var ocrDocs = await _ocrRepo.GetByPatientAsync(patient.Id);
        var ocrItems = ocrDocs.Select(d => new OcrDocumentSyncItem(
            d.Id, d.OpaqueInternalName, d.MimeType, d.DocumentType ?? "Unknown", d.PageCount,
            d.Sha256OfNormalized, d.SanitizedOcrPreview, d.ScannedAt));

        var history = await _historyRepo.GetByPatientAsync(patient.Id);
        var historyItems = history.Select(h => new MedicalHistorySyncItem(
            h.Id, h.SourceDocumentId, h.Title, h.MedicationName,
            h.Dosage, h.Frequency, h.Duration, h.Notes,
            h.Summary, h.Confidence, h.EventDate));

        return new MobileBootstrap(
            userDto,
            Patient: new BootstrapPatientDto(
                patient.Id,
                patient.UserId,
                patient.BloodType,
                patient.Allergies,
                patient.MedicalHistoryNotes,
                patient.Weight,
                patient.Height,
                patient.BloodPressureSystolic,
                patient.BloodPressureDiastolic,
                patient.Cholesterol,
                patient.Cnp),
            Vitals: vitalItems,
            Medications: medItems,
            SleepSessions: sleepItems,
            EnvironmentReadings: envItems,
            OcrDocuments: ocrItems,
            MedicalHistoryEntries: historyItems);
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
    public record MobileAuthResponse(bool Success, string? AccessToken = null, MobileBootstrap? Bootstrap = null, string? ErrorMessage = null);

    /// <summary>POST /api/mobile/auth/google — authenticate mobile user via Google id_token.</summary>
    [HttpPost("auth/google")]
    public async Task<ActionResult<MobileAuthResponse>> GoogleAuth([FromBody] MobileAuthRequest request)
    {
        try
        {
            var audiences = GetGoogleAudiences();
            if (audiences.Length == 0)
            {
                _logger.LogError("[MobileSync] Missing Google client id configuration. Set Google:MobileClientId (or Google:MobileClientIds) and/or Google:ClientId.");
                return StatusCode(500, new MobileAuthResponse(false, ErrorMessage: "Server misconfiguration: missing Google client id."));
            }

            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = audiences
            };
            var payload = await GoogleJsonWebSignature.ValidateAsync(request.GoogleIdToken, settings);

            var user = await _userRepo.GetByEmailAsync(payload.Email);
            if (user is null)
            {
                var transientUser = new User
                {
                    Id = StableGuid("mobile-auth", payload.Email),
                    Email = payload.Email,
                    Role = UserRole.Patient,
                    FirstName = payload.GivenName ?? string.Empty,
                    LastName = payload.FamilyName ?? string.Empty
                };

                _logger.LogInformation("[MobileSync] First login for {Email}: no cloud entities are auto-created.", payload.Email);
                var jwtForNewUser = GenerateJwt(transientUser);
                return Ok(new MobileAuthResponse(true, jwtForNewUser, Bootstrap: null));
            }

            var jwt = GenerateJwt(user);
            // Bootstrap cloud state for mobile hydration; patient may be null for first-time users.
            var bootstrap = await BuildBootstrapAsync(user);
            return Ok(new MobileAuthResponse(true, jwt, bootstrap));
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "[MobileSync] Invalid Google token");
            return Unauthorized(new MobileAuthResponse(false, ErrorMessage: "Invalid Google token"));
        }
    }

    public record UserProfileDto(Guid Id, string Email, int Role, string? FirstName, string? LastName,
        string? PhotoUrl, string? Phone, string? Address, string? City, string? Country, DateTime? DateOfBirth);

    /// <summary>GET /api/mobile/auth/me — get current user profile.</summary>
    [Authorize(AuthenticationSchemes = "Google")]
    [HttpGet("auth/me")]
    public async Task<IActionResult> GetCurrentProfile()
    {
        var user = await _userRepo.GetByEmailAsync(UserEmail);
        if (user is null) return NotFound();
        return Ok(new { User = new UserProfileDto(user.Id, user.Email, (int)user.Role, user.FirstName, user.LastName,
            user.PhotoUrl, user.Phone, user.Address, user.City, user.Country, user.DateOfBirth) });
    }

    /// <summary>
    /// GET /api/mobile/bootstrap — fetch all cloud data for the current user (one-shot sync seed).
    /// </summary>
    [Authorize(AuthenticationSchemes = "Google")]
    [HttpGet("bootstrap")]
    public async Task<ActionResult<MobileBootstrap>> Bootstrap()
    {
        var user = await _userRepo.GetByEmailAsync(UserEmail);
        if (user is null) return NotFound();
        return Ok(await BuildBootstrapAsync(user));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  User Sync
    // ═══════════════════════════════════════════════════════════════════════════

    public record SyncResult(bool Success, string? ErrorMessage = null);

    /// <summary>POST /api/mobile/sync/users/upsert — upsert user profile from device.</summary>
    [Authorize(AuthenticationSchemes = "Google")]
    [HttpPost("sync/users/upsert")]
    public async Task<ActionResult<SyncResult>> UpsertUser([FromBody] UpsertUserBody body)
    {
        var user = await _userRepo.GetByEmailAsync(UserEmail);
        var isNewUser = user is null;
        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = UserEmail,
                Role = UserRole.Patient,
                FirstName = body.User?.FirstName ?? string.Empty,
                LastName = body.User?.LastName ?? string.Empty,
            };
        }

        if (body.User is { } u)
        {
            user.FirstName = u.FirstName ?? user.FirstName;
            user.LastName = u.LastName ?? user.LastName;
            user.PhotoUrl = u.PhotoUrl ?? user.PhotoUrl;
            user.Phone = u.Phone ?? user.Phone;
            user.Address = u.Address ?? user.Address;
            user.City = u.City ?? user.City;
            user.Country = u.Country ?? user.Country;
            user.DateOfBirth = u.DateOfBirth ?? user.DateOfBirth;
            if (isNewUser)
            {
                await _userRepo.AddAsync(user);
            }
            else
            {
                await _userRepo.UpdateAsync(user);
            }
        }
        else if (isNewUser)
        {
            await _userRepo.AddAsync(user);
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
    [Authorize(AuthenticationSchemes = "Google")]
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
    [Authorize(AuthenticationSchemes = "Google")]
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
    [Authorize(AuthenticationSchemes = "Google")]
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
    [Authorize(AuthenticationSchemes = "Google")]
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
    [Authorize(AuthenticationSchemes = "Google")]
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
    [Authorize(AuthenticationSchemes = "Google")]
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
    [Authorize(AuthenticationSchemes = "Google")]
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
    [Authorize(AuthenticationSchemes = "Google")]
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
    [Authorize(AuthenticationSchemes = "Google")]
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
        Guid Id, string OpaqueInternalName, string MimeType, string DocumentType, int PageCount,
        string? Sha256OfNormalized, string SanitizedOcrPreview, DateTime ScannedAt);

    /// <summary>POST /api/mobile/sync/ocr-documents/upsert — push OCR document metadata.</summary>
    [Authorize(AuthenticationSchemes = "Google")]
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
            DocumentType = d.DocumentType,
            PageCount = d.PageCount,
            Sha256OfNormalized = d.Sha256OfNormalized ?? string.Empty,
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
    [Authorize(AuthenticationSchemes = "Google")]
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

    // ═══════════════════════════════════════════════════════════════════════════
    //  Doctor Assignments (read-only for patient)
    // ═══════════════════════════════════════════════════════════════════════════

    public record AssignedDoctorItem(Guid DoctorId, string FullName, string Email, string? PhotoUrl, DateTime AssignedAt, string? Notes);
    public record AssignedDoctorsResult(List<AssignedDoctorItem>? Doctors);

    /// <summary>GET /api/mobile/doctors/assigned — get doctors assigned to current patient.</summary>
    [Authorize(AuthenticationSchemes = "Google")]
    [HttpGet("doctors/assigned")]
    public async Task<ActionResult<AssignedDoctorsResult>> GetAssignedDoctors()
    {
        var email = UserEmail;
        var doctors = await _doctorAssignmentService.GetAssignedDoctorsAsync(email);

        var items = doctors.Select(d => new AssignedDoctorItem(
            d.DoctorId, d.FullName, d.Email, d.PhotoUrl, d.AssignedAt, d.Notes
        )).ToList();

        return Ok(new AssignedDoctorsResult(items));
    }

    /// <summary>
    /// POST /api/mobile/alerts/ecg — push a critical ECG triage alert to the cloud.
    /// The backend fans it out to the patient's assigned doctors as notifications.
    /// </summary>
    [Authorize(AuthenticationSchemes = "Google")]
    [HttpPost("alerts/ecg")]
    public async Task<ActionResult<SyncResult>> PostCriticalEcgAlert([FromBody] DeviceRequestEnvelope<CriticalAlertSyncItem> body)
    {
        try
        {
            var (user, patient) = await GetCurrentUserAndPatientAsync();

            var alert = body.Items?.FirstOrDefault();
            if (alert is null)
                return BadRequest(new SyncResult(false, "Missing alert payload."));

            var assignments = (await _assignments.GetByPatientIdAsync(patient.Id)).ToList();
            if (assignments.Count == 0)
            {
                _logger.LogInformation("[MobileSync] Critical alert received but patient has no assigned doctors. patientId={PatientId}", patient.Id);
                return Ok(new SyncResult(true));
            }

            var patientDisplay = string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName;
            var title = "Critical ECG alert";
            var bodyText = $"{patientDisplay}: {alert.Message}";

            foreach (var a in assignments)
            {
                await _notifications.AddAsync(new NotificationItem
                {
                    RecipientUserId = a.DoctorId,
                    RecipientRole = UserRole.Doctor,
                    Title = title,
                    Body = bodyText,
                    Type = NotificationType.CriticalAlert,
                    Severity = NotificationSeverity.Critical,
                    PatientId = patient.Id,
                    ActorUserId = user.Id,
                    ActorName = patientDisplay
                });
            }

            // Also store a copy for the patient inbox.
            await _notifications.AddAsync(new NotificationItem
            {
                RecipientUserId = user.Id,
                RecipientRole = UserRole.Patient,
                Title = title,
                Body = alert.Message,
                Type = NotificationType.CriticalAlert,
                Severity = NotificationSeverity.Critical,
                PatientId = patient.Id,
                ActorUserId = user.Id,
                ActorName = patientDisplay
            });

            _logger.LogWarning("[MobileSync] Critical ECG alert persisted. patientId={PatientId} doctorCount={DoctorCount} rule={Rule}",
                patient.Id, assignments.Count, alert.RuleName);

            return Ok(new SyncResult(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MobileSync] Failed to persist critical ECG alert");
            return StatusCode(500, new SyncResult(false, "Failed to persist critical alert."));
        }
    }
}
