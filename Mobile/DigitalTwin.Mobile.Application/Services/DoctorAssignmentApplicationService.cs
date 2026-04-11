using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

public class DoctorAssignmentApplicationService
{
    private readonly ICloudSyncService _cloudSync;
    private readonly IDoctorPatientAssignmentRepository _assignmentRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<DoctorAssignmentApplicationService> _logger;

    public DoctorAssignmentApplicationService(
        ICloudSyncService cloudSync,
        IDoctorPatientAssignmentRepository assignmentRepository,
        IUserRepository userRepository,
        ILogger<DoctorAssignmentApplicationService> logger)
    {
        _cloudSync = cloudSync;
        _assignmentRepository = assignmentRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<AssignedDoctorDto[]> GetAssignedDoctorsAsync()
    {
        try
        {
            var currentUser = await _userRepository.GetCurrentUserAsync();
            if (currentUser == null)
            {
                _logger.LogDebug("[DoctorAssignment] No current user in local DB; returning empty list.");
                return [];
            }

            var localDoctors = await _assignmentRepository.GetByUserIdAsync(currentUser.Id);

            if (!_cloudSync.IsAuthenticated)
            {
                _logger.LogDebug("[DoctorAssignment] Cloud auth not ready; returning local doctor assignments cache.");
                return Map(localDoctors);
            }

            var cloudDoctors = (await _cloudSync.GetAssignedDoctorsAsync()).ToArray();
            await _assignmentRepository.ReplaceForUserAsync(currentUser.Id, cloudDoctors);

            return Map(cloudDoctors);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DoctorAssignment] Failed to refresh from cloud. Falling back to local cache.");

            var currentUser = await _userRepository.GetCurrentUserAsync();
            if (currentUser == null)
            {
                return [];
            }

            var localDoctors = await _assignmentRepository.GetByUserIdAsync(currentUser.Id);
            return Map(localDoctors);
        }
    }

    private static AssignedDoctorDto[] Map(IEnumerable<DigitalTwin.Mobile.Domain.Models.AssignedDoctor> doctors)
    {
        return doctors.Select(d => new AssignedDoctorDto
        {
            DoctorId = d.DoctorId,
            FullName = d.FullName,
            Email = d.Email,
            PhotoUrl = d.PhotoUrl,
            AssignedAt = d.AssignedAt,
            Notes = d.Notes
        }).ToArray();
    }
}
