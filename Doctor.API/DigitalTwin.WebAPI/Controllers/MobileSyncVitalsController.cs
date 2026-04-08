using System.Security.Claims;
using DigitalTwin.Application.DTOs.MobileSync;
using DigitalTwin.Application.Enums;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalTwin.WebAPI.Controllers;

/// <summary>
/// Mobile sync endpoints for VitalSign data.
/// </summary>
[ApiController]
[Route("api/mobile/sync/vitals")]
[Authorize(Roles = "Patient")]
public class MobileSyncVitalsController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IPatientRepository _patients;
    private readonly IVitalSignRepository _vitals;
    private readonly ILogger<MobileSyncVitalsController> _logger;
    private readonly Dictionary<string, DateTime> _processedRequests = new();

    public MobileSyncVitalsController(
        IUserRepository users,
        IPatientRepository patients,
        IVitalSignRepository vitals,
        ILogger<MobileSyncVitalsController> logger)
    {
        _users = users;
        _patients = patients;
        _vitals = vitals;
        _logger = logger;
    }

    private string CurrentUserEmail =>
        User.FindFirstValue(ClaimTypes.Email)
        ?? throw new UnauthorizedAccessException("Email claim missing.");

    /// <summary>
    /// Appends vital signs to cloud storage (time-series data).
    /// </summary>
    [HttpPost("append")]
    public async Task<ActionResult<AppendVitalSignsResponse>> AppendVitals([FromBody] AppendVitalSignsRequest request)
    {
        var requestKey = $"{request.DeviceId}:{request.RequestId}";
        if (_processedRequests.ContainsKey(requestKey))
        {
            _logger.LogDebug("[VitalsSync] Duplicate request {RequestId} from device {DeviceId}", 
                request.RequestId, request.DeviceId);
            
            return Ok(new AppendVitalSignsResponse
            {
                AcceptedCount = 0,
                DedupedCount = request.Items.Count,
                RequestId = request.RequestId
            });
        }

        try
        {
            // Verify the patient exists and belongs to current user
            var currentUser = await _users.GetByEmailAsync(CurrentUserEmail);
            if (currentUser is null)
                return Unauthorized();

            var patient = await _patients.GetByIdAsync(request.PatientCloudId);
            if (patient is null || patient.UserId != currentUser.Id)
                return Forbid("Patient not found or not owned by current user.");

            var acceptedCount = 0;
            var dedupedCount = 0;

            foreach (var item in request.Items)
            {
                // Check for existing vital with same (PatientId, Type, Timestamp) - server-side dedup
                var existing = await _vitals.GetByPatientAsync(
                    request.PatientCloudId, 
                    type: MapTodomainVitalSignType(item.Type),
                    from: item.TimestampUtc.AddSeconds(-1), 
                    to: item.TimestampUtc.AddSeconds(1));

                if (existing.Any(v => v.Timestamp == item.TimestampUtc))
                {
                    dedupedCount++;
                    continue;
                }

                // Add new vital sign
                var vitalSign = new VitalSign
                {
                    PatientId = request.PatientCloudId,
                    Type = MapTodomainVitalSignType(item.Type),
                    Value = item.Value,
                    Unit = item.Unit,
                    Source = item.Source,
                    Timestamp = item.TimestampUtc
                };

                await _vitals.AddAsync(vitalSign);
                acceptedCount++;
            }

            _processedRequests[requestKey] = DateTime.UtcNow;

            _logger.LogInformation("[VitalsSync] Processed {Total} vitals for patient {PatientId}: {Accepted} accepted, {Deduped} deduped", 
                request.Items.Count, request.PatientCloudId, acceptedCount, dedupedCount);

            return Ok(new AppendVitalSignsResponse
            {
                AcceptedCount = acceptedCount,
                DedupedCount = dedupedCount,
                RequestId = request.RequestId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VitalsSync] Failed to append vitals for patient {PatientId}", request.PatientCloudId);
            
            return Ok(new AppendVitalSignsResponse
            {
                AcceptedCount = 0,
                DedupedCount = 0,
                ErrorMessage = "Failed to sync vital signs",
                RequestId = request.RequestId
            });
        }
    }

    /// <summary>
    /// Gets vital signs from cloud storage.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<GetVitalSignsResponse>> GetVitals([FromQuery] GetVitalSignsRequest request)
    {
        try
        {
            var currentUser = await _users.GetByEmailAsync(CurrentUserEmail);
            if (currentUser is null)
                return Unauthorized();

            var patient = await _patients.GetByUserIdAsync(currentUser.Id);
            if (patient is null)
                return NotFound("Patient profile not found.");

            var fromUtc = request.FromUtc ?? DateTime.UtcNow.AddDays(-7);
            var toUtc = request.ToUtc ?? DateTime.UtcNow;
            Domain.Enums.VitalSignType? type = request.Type.HasValue ? MapTodomainVitalSignType(request.Type.Value) : null;

            var vitals = await _vitals.GetByPatientAsync(patient.Id, type, fromUtc, toUtc);

            var items = vitals.Select(v => new VitalSignSyncDto
            {
                Type = MapToApplicationVitalSignType(v.Type),
                Value = v.Value,
                Unit = v.Unit,
                Source = v.Source,
                TimestampUtc = v.Timestamp
            }).ToList();

            return Ok(new GetVitalSignsResponse
            {
                Items = items,
                RequestId = string.Empty
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VitalsSync] Failed to get vitals for user {Email}", CurrentUserEmail);
            return StatusCode(500, "Failed to retrieve vital signs");
        }
    }

    private static Domain.Enums.VitalSignType MapTodomainVitalSignType(VitalSignType appType)
    {
        return (Domain.Enums.VitalSignType)(int)appType;
    }

    private static VitalSignType MapToApplicationVitalSignType(Domain.Enums.VitalSignType domainType)
    {
        return (VitalSignType)(int)domainType;
    }
}