using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Sync;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Domain.Sync.Drainers;

/// <summary>
/// Bidirectional sync for <c>Patient</c> rows.
/// PUSH: dirty local patients → cloud (upsert by cloud UserId).
/// PULL: refresh local patient profiles from cloud.
/// Must run after <see cref="UserSyncDrainer"/>.
/// </summary>
public sealed class PatientSyncDrainer : SyncDrainerBase<Patient>
{
    private readonly IPatientRepository _local;
    private readonly IPatientSyncClient? _syncClient;

    public override int Order => 1;
    public override string TableName => "Patients";
    protected override bool IsCloudConfigured => _syncClient is not null;

    public PatientSyncDrainer(
        IPatientRepository local,
        IPatientSyncClient? syncClient,
        ILogger<PatientSyncDrainer> logger) : base(logger)
    {
        _local = local;
        _syncClient = syncClient;
    }

    // ── Push hooks ───────────────────────────────────────────────────────────

    protected override async Task<List<Patient>> GetDirtyItemsAsync(CancellationToken ct)
        => (await _local.GetDirtyAsync()).ToList();

    protected override Task<List<Patient>> MapToCloudBatchAsync(List<Patient> dirtyItems, CancellationToken ct)
        => Task.FromResult(dirtyItems);

    protected override async Task UpsertToCloudBatchAsync(List<Patient> cloudItems, CancellationToken ct)
    {
        foreach (var patient in cloudItems)
        {
            ct.ThrowIfCancellationRequested();
            
            var success = await _syncClient!.PushPatientAsync(patient, ct);
            if (!success)
            {
                Logger.LogError("[PatientSyncDrainer] Failed to sync patient for user {UserId}", patient.UserId);
                throw new InvalidOperationException($"Failed to sync patient for user {patient.UserId}");
            }
        }
    }

    protected override async Task MarkPushedAsSyncedAsync(List<Patient> originalDirtyItems, CancellationToken ct)
        => await _local.MarkSyncedAsync(originalDirtyItems);

    protected override async Task PurgeSyncedAsync(CancellationToken ct)
        => await _local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);

    // ── Pull hooks ───────────────────────────────────────────────────────────

    protected override async Task<IReadOnlyList<PullScope>> GetPullScopesAsync(CancellationToken ct)
    {
        // For patient sync, we'll pull the current user's patient profile
        var localPatients = (await _local.GetAllAsync()).ToList();
        if (localPatients.Count == 0) return [];

        // Assume single patient profile for mobile app
        var localPatient = localPatients[0];
        return [new PullScope(localPatient.Id, localPatient.Id, localPatient)];
    }

    protected override async Task<IReadOnlyList<Patient>> FetchCloudItemsAsync(PullScope scope, CancellationToken ct)
    {
        try
        {
            var localPatient = (Patient)scope.Context!;
            var cloudPatient = await _syncClient!.PullPatientProfileAsync(localPatient.Id, localPatient.UserId, ct);
            
            return cloudPatient != null ? [cloudPatient] : [];
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[PatientSyncDrainer] Failed to fetch patient profile from cloud");
            return [];
        }
    }

    protected override async Task<int> MergeCloudItemsToLocalAsync(IReadOnlyList<Patient> cloudItems, PullScope scope, CancellationToken ct)
    {
        if (cloudItems.Count == 0) return 0;

        var cloudPatient = cloudItems[0];
        var localPatient = (Patient)scope.Context!;

        // Null-coalescing merge: prefer the cloud value when available, otherwise keep the
        // existing local value. This prevents a stale/incomplete cloud record from
        // overwriting locally-set fields that haven't been pushed yet.
        localPatient.BloodType                = cloudPatient.BloodType                ?? localPatient.BloodType;
        localPatient.Allergies                = cloudPatient.Allergies                ?? localPatient.Allergies;
        localPatient.MedicalHistoryNotes      = cloudPatient.MedicalHistoryNotes      ?? localPatient.MedicalHistoryNotes;
        localPatient.Weight                   = cloudPatient.Weight                   ?? localPatient.Weight;
        localPatient.Height                   = cloudPatient.Height                   ?? localPatient.Height;
        localPatient.BloodPressureSystolic    = cloudPatient.BloodPressureSystolic    ?? localPatient.BloodPressureSystolic;
        localPatient.BloodPressureDiastolic   = cloudPatient.BloodPressureDiastolic   ?? localPatient.BloodPressureDiastolic;
        localPatient.Cholesterol              = cloudPatient.Cholesterol              ?? localPatient.Cholesterol;
        localPatient.Cnp                      = cloudPatient.Cnp                      ?? localPatient.Cnp;
        await _local.UpdateAsync(localPatient);
        // UpdateAsync on the local repo sets IsDirty=true (markDirtyOnInsert:true).
        // A pull from cloud is not a local change — clear the flag immediately.
        await _local.MarkSyncedAsync([localPatient]);
        return 1;
    }

}
