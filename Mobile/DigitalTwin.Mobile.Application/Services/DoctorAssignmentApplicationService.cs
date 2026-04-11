using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

public class DoctorAssignmentApplicationService
{
    private readonly ICloudSyncService _cloudSync;
    private readonly ILogger<DoctorAssignmentApplicationService> _logger;

    public DoctorAssignmentApplicationService(
        ICloudSyncService cloudSync,
        ILogger<DoctorAssignmentApplicationService> logger)
    {
        _cloudSync = cloudSync;
        _logger = logger;
    }

    public async Task<AssignedDoctorDto[]> GetAssignedDoctorsAsync()
    {
        try
        {
            if (!_cloudSync.IsAuthenticated)
            {
                _logger.LogDebug("[DoctorAssignment] Skipping cloud doctor assignment fetch because authentication is not ready.");
                return [];
            }

            var doctors = await _cloudSync.GetAssignedDoctorsAsync();
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DoctorAssignment] Failed to load");
            return [];
        }
    }
}
