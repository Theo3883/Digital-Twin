using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Domain.Services;

/// <summary>
/// Domain service for coordinating incremental data synchronization
/// Uses checkpoints to avoid fetching all data on every sync
/// </summary>
public class SyncService
{
    private readonly IUserRepository _userRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IVitalSignRepository _vitalSignRepository;
    private readonly IDoctorPatientAssignmentRepository _doctorAssignmentRepository;
    private readonly ICloudSyncService _cloudSyncService;
    private readonly ISyncStateService _syncStateService;
    private readonly ILogger<SyncService> _logger;
    private bool _cloudReachable = true;

    private const string SyncEntity_Patient = "Patient";
    private const string SyncEntity_VitalSigns = "VitalSigns";
    private const string SyncEntity_DoctorAssignments = "DoctorAssignments";
    private const int FallbackSyncWindowDays = 7; // Default if no checkpoint exists
    private const int VitalSignsPushBatchSize = 500;

    public SyncService(
        IUserRepository userRepository,
        IPatientRepository patientRepository,
        IVitalSignRepository vitalSignRepository,
        IDoctorPatientAssignmentRepository doctorAssignmentRepository,
        ICloudSyncService cloudSyncService,
        ISyncStateService syncStateService,
        ILogger<SyncService> logger)
    {
        _userRepository = userRepository;
        _patientRepository = patientRepository;
        _vitalSignRepository = vitalSignRepository;
        _doctorAssignmentRepository = doctorAssignmentRepository;
        _cloudSyncService = cloudSyncService;
        _syncStateService = syncStateService;
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

            _cloudReachable = await _cloudSyncService.IsCloudReachableAsync();
            if (!_cloudReachable)
            {
                _logger.LogWarning("[SyncService] Cloud health check failed — skipping sync, using local data.");
                return true;
            }

            _logger.LogInformation("[SyncService] Starting full sync");

            // Push local changes FIRST, then pull cloud updates
            // This avoids write-write race conditions where push and pull operate on same rows simultaneously
            await PushLocalChangesAsync();
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

        // Early exit: check if anything needs pushing
        var unsyncedUsers = await _userRepository.GetUnsyncedAsync();
        var unsyncedPatients = await _patientRepository.GetUnsyncedAsync();
        var unsyncedVitals = await _vitalSignRepository.GetUnsyncedAsync();

        if (!unsyncedUsers.Any() && !unsyncedPatients.Any() && !unsyncedVitals.Any())
        {
            _logger.LogDebug("[SyncService] Nothing to push");
            return;
        }

        // Push in parallel for better performance
        await Task.WhenAll(
            PushUsersAsync(unsyncedUsers),
            PushPatientsAsync(unsyncedPatients),
            PushVitalSignsAsync(unsyncedVitals)
        );
    }

    private async Task PushUsersAsync(IEnumerable<Models.User> unsyncedUsers)
    {
        foreach (var user in unsyncedUsers)
        {
            try
            {
                if (await _cloudSyncService.SyncUserAsync(user))
                {
                    await _userRepository.MarkAsSyncedAsync(user.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SyncService] Failed to push user {UserId}", user.Id);
            }
        }
    }

    private async Task PushPatientsAsync(IEnumerable<Models.Patient> unsyncedPatients)
    {
        foreach (var patient in unsyncedPatients)
        {
            try
            {
                if (await _cloudSyncService.SyncPatientAsync(patient))
                {
                    await _patientRepository.MarkAsSyncedAsync(patient.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SyncService] Failed to push patient {PatientId}", patient.Id);
            }
        }
    }

    private async Task PushVitalSignsAsync(IEnumerable<Models.VitalSign> unsyncedVitals)
    {
        var pendingVitals = unsyncedVitals.ToList();
        if (!pendingVitals.Any())
            return;

        try
        {
            var syncedIds = new List<Guid>(pendingVitals.Count);

            foreach (var batch in pendingVitals.Chunk(VitalSignsPushBatchSize))
            {
                if (!await _cloudSyncService.SyncVitalSignsAsync(batch))
                {
                    _logger.LogWarning("[SyncService] Failed to push vital signs batch of {BatchSize}", batch.Length);
                    continue;
                }

                syncedIds.AddRange(batch.Select(v => v.Id));
            }

            if (syncedIds.Count > 0)
            {
                await _vitalSignRepository.MarkAsSyncedAsync(syncedIds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SyncService] Failed to push vital signs");
        }
    }

    /// <summary>
    /// Pulls updates from cloud using incremental/delta sync based on last successful checkpoint
    /// </summary>
    public async Task PullCloudUpdatesAsync()
    {
        if (!CanSyncWithCloud())
            return;

        // Fetch current user/patient data once (needed for all pulls)
        var currentUser = await _userRepository.GetCurrentUserAsync();
        var currentPatient = await _patientRepository.GetCurrentPatientAsync();

        if (currentUser == null || currentPatient == null)
            return;

        // Get sync checkpoints (or use fallback window)
        var patientCheckpoint = await _syncStateService.GetLastSyncTimeAsync(SyncEntity_Patient);
        var vitalsCheckpoint = await _syncStateService.GetLastSyncTimeAsync(SyncEntity_VitalSigns);
        var assignmentsCheckpoint = await _syncStateService.GetLastSyncTimeAsync(SyncEntity_DoctorAssignments);

        // Determine sync windows: use checkpoint if available, otherwise use fallback
        var patientFromDate = patientCheckpoint ?? DateTime.UtcNow.AddDays(-FallbackSyncWindowDays);
        var vitalsFromDate = vitalsCheckpoint ?? DateTime.UtcNow.AddDays(-FallbackSyncWindowDays);
        var assignmentsFromDate = assignmentsCheckpoint ?? DateTime.UtcNow.AddDays(-FallbackSyncWindowDays);

        _logger.LogInformation("[SyncService] Starting incremental pull - Patient: {PatientFrom}, Vitals: {VitalsFrom}, Assignments: {AssignmentsFrom}",
            patientFromDate, vitalsFromDate, assignmentsFromDate);

        // Fetch each independently — one failure shouldn't block saving other entities.
        var cloudPatient = await SafeFetchNullableAsync(
            () => _cloudSyncService.GetPatientProfileAsync(),
            "Patient");

        var cloudVitals = await SafeFetchEnumerableAsync(
            () => _cloudSyncService.GetVitalSignsAsync(vitalsFromDate),
            "VitalSigns");

        var cloudAssignments = await SafeFetchEnumerableAsync(
            () => _cloudSyncService.GetAssignedDoctorsAsync(),
            "DoctorAssignments");

        // Merge & checkpoint independently — persist partial results.
        var now = DateTime.UtcNow;

        if (cloudPatient != null)
        {
            await MergePatientDataAsync(currentPatient, cloudPatient);
            await _syncStateService.SetLastSyncTimeAsync(SyncEntity_Patient, now);
        }

        if (cloudVitals != null)
        {
            await MergeVitalSignsAsync(currentPatient, cloudVitals, vitalsFromDate);
            await _syncStateService.SetLastSyncTimeAsync(SyncEntity_VitalSigns, now);
        }

        if (cloudAssignments != null)
        {
            await MergeDoctorAssignmentsAsync(currentUser, cloudAssignments);
            await _syncStateService.SetLastSyncTimeAsync(SyncEntity_DoctorAssignments, now);
        }

        _logger.LogInformation("[SyncService] Incremental pull completed and checkpoints updated");
    }

    /// <summary>
    /// Fetches data from cloud with independent error handling.
    /// Returns null on failure instead of throwing — so other fetches can still proceed.
    /// </summary>
    private async Task<T?> SafeFetchNullableAsync<T>(Func<Task<T?>> fetchFunc, string entityName) where T : class
    {
        try
        {
            return await fetchFunc();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[SyncService] Failed to pull {Entity} from cloud — local data preserved",
                entityName);
            return null;
        }
    }

    /// <summary>
    /// Overload for enumerable results — returns null on failure.
    /// </summary>
    private async Task<IEnumerable<T>?> SafeFetchEnumerableAsync<T>(
        Func<Task<IEnumerable<T>>> fetchFunc,
        string entityName)
    {
        try
        {
            return await fetchFunc();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[SyncService] Failed to pull {Entity} from cloud — local data preserved",
                entityName);
            return null;
        }
    }

    private async Task MergePatientDataAsync(Models.Patient localPatient, Patient? cloudPatient)
    {
        if (cloudPatient == null)
            return;

        try
        {
            MergePatientData(localPatient, cloudPatient);
            await _patientRepository.SaveAsync(localPatient);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SyncService] Failed to merge patient data");
        }
    }

    private async Task MergeVitalSignsAsync(Models.Patient currentPatient, IEnumerable<VitalSign>? cloudVitals, DateTime fromDate)
    {
        if (cloudVitals == null || !cloudVitals.Any())
            return;

        try
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SyncService] Failed to merge vital signs");
        }
    }

    private async Task MergeDoctorAssignmentsAsync(Models.User currentUser, IEnumerable<AssignedDoctor>? cloudAssignments)
    {
        try
        {
            if (cloudAssignments?.Any() == true)
            {
                await _doctorAssignmentRepository.ReplaceForUserAsync(currentUser.Id, cloudAssignments);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SyncService] Failed to merge doctor assignments");
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

    /// <summary>
    /// Resets all sync checkpoints to force a full resync on next pull
    /// Useful for recovery or when data integrity is suspect
    /// </summary>
    public async Task ResetSyncCheckpointsAsync()
    {
        try
        {
            await _syncStateService.ResetAllCheckpointsAsync();
            _logger.LogWarning("[SyncService] All sync checkpoints have been reset - next sync will be a full resync");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SyncService] Failed to reset sync checkpoints");
        }
    }

    /// <summary>
    /// Get diagnostic information about current sync state
    /// </summary>
    public async Task<Dictionary<string, DateTime?>> GetSyncStateAsync()
    {
        try
        {
            var state = await _syncStateService.GetAllSyncStatesAsync();
            _logger.LogInformation("[SyncService] Sync state: {@SyncState}", state);
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SyncService] Failed to get sync state");
            return new();
        }
    }
}