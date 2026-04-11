using DigitalTwin.Mobile.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Domain.Services;

/// <summary>
/// Domain service for coordinating data synchronization
/// </summary>
public class SyncService
{
    private readonly IUserRepository _userRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IVitalSignRepository _vitalSignRepository;
    private readonly ICloudSyncService _cloudSyncService;
    private readonly ILogger<SyncService> _logger;

    public SyncService(
        IUserRepository userRepository,
        IPatientRepository patientRepository,
        IVitalSignRepository vitalSignRepository,
        ICloudSyncService cloudSyncService,
        ILogger<SyncService> logger)
    {
        _userRepository = userRepository;
        _patientRepository = patientRepository;
        _vitalSignRepository = vitalSignRepository;
        _cloudSyncService = cloudSyncService;
        _logger = logger;
    }

    /// <summary>
    /// Performs full bidirectional sync with cloud
    /// </summary>
    public async Task<bool> PerformFullSyncAsync()
    {
        try
        {
            if (!CanSyncWithCloud())
                return true;

            _logger.LogInformation("[SyncService] Starting full sync");

            // 1. Push local changes to cloud
            await PushLocalChangesAsync();

            // 2. Pull updates from cloud
            await PullCloudUpdatesAsync();

            _logger.LogInformation("[SyncService] Full sync completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SyncService] Full sync failed");
            return false;
        }
    }

    /// <summary>
    /// Pushes local unsynced data to cloud
    /// </summary>
    public async Task PushLocalChangesAsync()
    {
        if (!CanSyncWithCloud())
            return;

        // Push users
        var unsyncedUsers = await _userRepository.GetUnsyncedAsync();
        foreach (var user in unsyncedUsers)
        {
            if (await _cloudSyncService.SyncUserAsync(user))
            {
                await _userRepository.MarkAsSyncedAsync(user.Id);
            }
        }

        // Push patients
        var unsyncedPatients = await _patientRepository.GetUnsyncedAsync();
        foreach (var patient in unsyncedPatients)
        {
            if (await _cloudSyncService.SyncPatientAsync(patient))
            {
                await _patientRepository.MarkAsSyncedAsync(patient.Id);
            }
        }

        // Push vital signs
        var unsyncedVitals = await _vitalSignRepository.GetUnsyncedAsync();
        if (unsyncedVitals.Any())
        {
            if (await _cloudSyncService.SyncVitalSignsAsync(unsyncedVitals))
            {
                await _vitalSignRepository.MarkAsSyncedAsync(unsyncedVitals.Select(v => v.Id));
            }
        }
    }

    /// <summary>
    /// Pulls updates from cloud
    /// </summary>
    public async Task PullCloudUpdatesAsync()
    {
        if (!CanSyncWithCloud())
            return;

        // Pull patient profile updates
        var cloudPatient = await _cloudSyncService.GetPatientProfileAsync();
        if (cloudPatient != null)
        {
            var localPatient = await _patientRepository.GetCurrentPatientAsync();
            if (localPatient != null)
            {
                // Merge cloud updates with local data (null-coalescing merge)
                MergePatientData(localPatient, cloudPatient);
                await _patientRepository.SaveAsync(localPatient);
            }
        }

        // Pull recent vital signs (last 7 days)
        var fromDate = DateTime.UtcNow.AddDays(-7);
        var cloudVitals = await _cloudSyncService.GetVitalSignsAsync(fromDate);
        
        if (cloudVitals.Any())
        {
            var currentPatient = await _patientRepository.GetCurrentPatientAsync();
            if (currentPatient != null)
            {
                // Filter out vitals that already exist locally
                var existingVitals = await _vitalSignRepository.GetByPatientIdAsync(currentPatient.Id, fromDate);
                var existingTimestamps = existingVitals.Select(v => v.Timestamp).ToHashSet();
                
                var newVitals = cloudVitals
                    .Where(v => !existingTimestamps.Contains(v.Timestamp))
                    .Select(v => new Models.VitalSign
                    {
                        Id = Guid.NewGuid(),
                        PatientId = currentPatient.Id,
                        Type = v.Type,
                        Value = v.Value,
                        Unit = v.Unit,
                        Source = v.Source,
                        Timestamp = v.Timestamp,
                        IsSynced = true // Already from cloud
                    });

                if (newVitals.Any())
                {
                    await _vitalSignRepository.SaveRangeAsync(newVitals);
                }
            }
        }
    }

    private static void MergePatientData(Models.Patient local, Models.Patient cloud)
    {
        // Null-coalescing merge: prefer cloud values when available, otherwise keep local
        local.BloodType = cloud.BloodType ?? local.BloodType;
        local.Allergies = cloud.Allergies ?? local.Allergies;
        local.MedicalHistoryNotes = cloud.MedicalHistoryNotes ?? local.MedicalHistoryNotes;
        local.Weight = cloud.Weight ?? local.Weight;
        local.Height = cloud.Height ?? local.Height;
        local.BloodPressureSystolic = cloud.BloodPressureSystolic ?? local.BloodPressureSystolic;
        local.BloodPressureDiastolic = cloud.BloodPressureDiastolic ?? local.BloodPressureDiastolic;
        local.Cholesterol = cloud.Cholesterol ?? local.Cholesterol;
        local.Cnp = cloud.Cnp ?? local.Cnp;
        local.UpdatedAt = DateTime.UtcNow;
    }

    private bool CanSyncWithCloud()
    {
        if (_cloudSyncService.IsAuthenticated)
            return true;

        _logger.LogDebug("[SyncService] Skipping cloud sync because authentication is not ready.");
        return false;
    }
}