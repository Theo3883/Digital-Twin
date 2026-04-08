using System.Security.Claims;
using DigitalTwin.Application.DTOs.MobileSync;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalTwin.WebAPI.Controllers;

/// <summary>
/// Mobile sync endpoints for Patient data.
/// </summary>
[ApiController]
[Route("api/mobile/sync/patients")]
[Authorize(Roles = "Patient")]
public class MobileSyncPatientsController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IPatientRepository _patients;
    private readonly ILogger<MobileSyncPatientsController> _logger;
    private readonly Dictionary<string, DateTime> _processedRequests = new();

    public MobileSyncPatientsController(
        IUserRepository users,
        IPatientRepository patients,
        ILogger<MobileSyncPatientsController> logger)
    {
        _users = users;
        _patients = patients;
        _logger = logger;
    }

    private string CurrentUserEmail =>
        User.FindFirstValue(ClaimTypes.Email)
        ?? throw new UnauthorizedAccessException("Email claim missing.");

    /// <summary>
    /// Upserts patient data to cloud storage.
    /// </summary>
    [HttpPost("upsert")]
    public async Task<ActionResult<UpsertPatientResponse>> UpsertPatient([FromBody] UpsertPatientRequest request)
    {
        var requestKey = $"{request.DeviceId}:{request.RequestId}";
        if (_processedRequests.ContainsKey(requestKey))
        {
            _logger.LogDebug("[PatientSync] Duplicate request {RequestId} from device {DeviceId}", 
                request.RequestId, request.DeviceId);
            
            return Ok(new UpsertPatientResponse
            {
                Success = true,
                RequestId = request.RequestId
            });
        }

        try
        {
            // Get current user to verify ownership
            var currentUser = await _users.GetByEmailAsync(CurrentUserEmail);
            if (currentUser is null)
                return Unauthorized();

            // Find existing patient by cloud user ID
            var existing = await _patients.GetByUserIdAsync(currentUser.Id);
            Guid cloudPatientId;

            if (existing is not null)
            {
                // Update existing patient (null-coalescing merge as per current drainer logic)
                existing.BloodType = request.Patient.BloodType ?? existing.BloodType;
                existing.Allergies = request.Patient.Allergies ?? existing.Allergies;
                existing.MedicalHistoryNotes = request.Patient.MedicalHistoryNotes ?? existing.MedicalHistoryNotes;
                existing.Weight = request.Patient.Weight ?? existing.Weight;
                existing.Height = request.Patient.Height ?? existing.Height;
                existing.BloodPressureSystolic = request.Patient.BloodPressureSystolic ?? existing.BloodPressureSystolic;
                existing.BloodPressureDiastolic = request.Patient.BloodPressureDiastolic ?? existing.BloodPressureDiastolic;
                existing.Cholesterol = request.Patient.Cholesterol ?? existing.Cholesterol;
                existing.Cnp = request.Patient.Cnp ?? existing.Cnp;
                existing.UpdatedAt = DateTime.UtcNow;

                await _patients.UpdateAsync(existing);
                cloudPatientId = existing.Id;

                _logger.LogInformation("[PatientSync] Updated patient for user {Email} (CloudPatientId={CloudPatientId})", 
                    CurrentUserEmail, cloudPatientId);
            }
            else
            {
                // Create new patient
                var newPatient = new Patient
                {
                    UserId = currentUser.Id,
                    BloodType = request.Patient.BloodType,
                    Allergies = request.Patient.Allergies,
                    MedicalHistoryNotes = request.Patient.MedicalHistoryNotes,
                    Weight = request.Patient.Weight,
                    Height = request.Patient.Height,
                    BloodPressureSystolic = request.Patient.BloodPressureSystolic,
                    BloodPressureDiastolic = request.Patient.BloodPressureDiastolic,
                    Cholesterol = request.Patient.Cholesterol,
                    Cnp = request.Patient.Cnp,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _patients.AddAsync(newPatient);
                cloudPatientId = newPatient.Id;

                _logger.LogInformation("[PatientSync] Created patient for user {Email} (CloudPatientId={CloudPatientId})", 
                    CurrentUserEmail, cloudPatientId);
            }

            _processedRequests[requestKey] = DateTime.UtcNow;

            return Ok(new UpsertPatientResponse
            {
                Success = true,
                CloudPatientId = cloudPatientId,
                RequestId = request.RequestId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PatientSync] Failed to upsert patient for user {Email}", CurrentUserEmail);
            
            return Ok(new UpsertPatientResponse
            {
                Success = false,
                ErrorMessage = "Failed to sync patient data",
                RequestId = request.RequestId
            });
        }
    }

    /// <summary>
    /// Gets patient profile data from cloud.
    /// </summary>
    [HttpGet("profile")]
    public async Task<ActionResult<GetPatientProfileResponse>> GetProfile()
    {
        try
        {
            var currentUser = await _users.GetByEmailAsync(CurrentUserEmail);
            if (currentUser is null)
                return Unauthorized();

            var patient = await _patients.GetByUserIdAsync(currentUser.Id);

            return Ok(new GetPatientProfileResponse
            {
                Patient = patient is null ? null : new PatientSyncDto
                {
                    Id = patient.Id,
                    UserId = patient.UserId,
                    BloodType = patient.BloodType,
                    Allergies = patient.Allergies,
                    MedicalHistoryNotes = patient.MedicalHistoryNotes,
                    Weight = patient.Weight,
                    Height = patient.Height,
                    BloodPressureSystolic = patient.BloodPressureSystolic,
                    BloodPressureDiastolic = patient.BloodPressureDiastolic,
                    Cholesterol = patient.Cholesterol,
                    Cnp = patient.Cnp,
                    UpdatedAtUtc = patient.UpdatedAt
                },
                RequestId = string.Empty
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PatientSync] Failed to get patient profile for user {Email}", CurrentUserEmail);
            return StatusCode(500, "Failed to retrieve patient profile");
        }
    }
}