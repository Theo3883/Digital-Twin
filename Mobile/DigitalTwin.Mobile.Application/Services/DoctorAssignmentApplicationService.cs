using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

public class DoctorAssignmentApplicationService
{
    private readonly IDoctorPatientAssignmentRepository _localRepo;
    private readonly IUserRepository _userRepository;
    private readonly ICloudSyncService _cloudSyncService;
    private readonly ILogger<DoctorAssignmentApplicationService> _logger;

    public DoctorAssignmentApplicationService(
        IDoctorPatientAssignmentRepository localRepo,
        IUserRepository userRepository,
        ICloudSyncService cloudSyncService,
        ILogger<DoctorAssignmentApplicationService> logger)
    {
        _localRepo = localRepo;
        _userRepository = userRepository;
        _cloudSyncService = cloudSyncService;
        _logger = logger;
    }

    public async Task<AssignedDoctorDto[]> GetAssignedDoctorsAsync()
    {
        var currentUser = await _userRepository.GetCurrentUserAsync();
        if (currentUser == null)
        {
            _logger.LogDebug("[DoctorAssignment] No current user — returning empty");
            return Array.Empty<AssignedDoctorDto>();
        }

        // ── STEP 1: Always read from LOCAL DB first (instant) ──
        var localDoctors = await _localRepo.GetByUserIdAsync(currentUser.Id);

        _logger.LogDebug(
            "[DoctorAssignment] Retrieved {Count} assigned doctors from local DB for user {UserId}",
            localDoctors.Count, currentUser.Id);

        _logger.LogDebug(
            "[DoctorAssignment] Local doctor data is {Status}",
            localDoctors.Any() ? "available" : "empty");

        
        _logger.LogInformation(
            "[DoctorAssignment] Loaded {Count} assigned doctors from local DB", 
            localDoctors.Count);

        // ── STEP 2: Background refresh from cloud (non-blocking) ──
        _ = RefreshFromCloudAsync(currentUser.Id);

        // ── STEP 3: Return local data immediately ──
        return localDoctors.Select(d => new AssignedDoctorDto
        {
            DoctorId = d.DoctorId,
            FullName = d.FullName,
            Email = d.Email,
            PhotoUrl = d.PhotoUrl,
            AssignedAt = d.AssignedAt,
            Notes = d.Notes
        }).ToArray();
    }

    /// <summary>
    /// Fire-and-forget cloud refresh. Updates local DB silently.
    /// Next time GetAssignedDoctorsAsync is called, fresh data will be there.
    /// </summary>
    private async Task RefreshFromCloudAsync(Guid userId)
    {
        try
        {
            if (!_cloudSyncService.IsAuthenticated)
                return;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var cloudDoctors = await _cloudSyncService.GetAssignedDoctorsAsync();

            if (cloudDoctors?.Any() == true)
            {
                await _localRepo.ReplaceForUserAsync(userId, cloudDoctors);
                _logger.LogInformation(
                    "[DoctorAssignment] Cloud refresh: updated {Count} doctors in local DB",
                    cloudDoctors.Count());
            }
        }
        catch (Exception ex)
        {
            // Non-critical — local data is already displayed
            _logger.LogDebug(ex, 
                "[DoctorAssignment] Cloud refresh failed — local data still valid");
        }
    }
}