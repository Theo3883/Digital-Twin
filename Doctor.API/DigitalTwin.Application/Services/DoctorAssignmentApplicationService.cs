using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Returns doctor assignments for a patient by mapping domain assignment data to DTOs.
/// </summary>
public class DoctorAssignmentApplicationService : IDoctorAssignmentApplicationService
{
    private readonly IDoctorPatientAssignmentService _assignmentService;
    private readonly ILogger<DoctorAssignmentApplicationService> _logger;
    private readonly ICloudHealthService? _cloudHealth;

    /// <summary>
    /// Initializes a new instance of the <see cref="DoctorAssignmentApplicationService"/> class.
    /// </summary>
    public DoctorAssignmentApplicationService(
        IDoctorPatientAssignmentService assignmentService,
        ILogger<DoctorAssignmentApplicationService> logger,
        ICloudHealthService? cloudHealth = null)
    {
        _assignmentService = assignmentService;
        _logger = logger;
        _cloudHealth = cloudHealth;
    }

    /// <summary>
    /// Gets the doctors assigned to the patient identified by email.
    /// Returns an empty list immediately when the cloud database is known to be
    /// unreachable (circuit open), or after a connection failure is detected.
    /// </summary>
    public async Task<IEnumerable<AssignedDoctorDto>> GetAssignedDoctorsAsync(string patientEmail)
    {
        // Skip the query immediately if the circuit breaker knows the cloud is down.
        if (_cloudHealth is not null && !await _cloudHealth.IsAvailableAsync())
        {
            _logger.LogDebug("[DoctorAssignment] Cloud circuit open — skipping doctor-assignment query.");
            return Enumerable.Empty<AssignedDoctorDto>();
        }

        try
        {
            var assigned = await _assignmentService.GetAssignedDoctorsAsync(patientEmail);
            return assigned.Select(a => new AssignedDoctorDto
            {
                DoctorId = a.Doctor.Id,
                FullName = $"{a.Doctor.FirstName} {a.Doctor.LastName}".Trim(),
                Email = a.Doctor.Email,
                PhotoUrl = a.Doctor.PhotoUrl,
                AssignedAt = a.AssignedAt,
                Notes = a.Notes
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DoctorAssignment] Could not load doctor assignments — cloud unavailable. Returning empty list.");
            // Trip the circuit breaker so subsequent calls skip without waiting.
            _cloudHealth?.ReportFailure();
            return Enumerable.Empty<AssignedDoctorDto>();
        }
    }
}

