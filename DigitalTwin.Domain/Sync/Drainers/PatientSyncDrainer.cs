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
    private readonly IPatientRepository? _cloud;
    private readonly ICloudIdentityResolver _identityResolver;

    public override int Order => 1;
    public override string TableName => "Patients";
    protected override bool IsCloudConfigured => _cloud is not null;

    public PatientSyncDrainer(
        IPatientRepository local,
        IPatientRepository? cloud,
        ICloudIdentityResolver identityResolver,
        ILogger<PatientSyncDrainer> logger) : base(logger)
    {
        _local = local;
        _cloud = cloud;
        _identityResolver = identityResolver;
    }

    // ── Push hooks ───────────────────────────────────────────────────────────

    protected override async Task<List<Patient>> GetDirtyItemsAsync(CancellationToken ct)
        => (await _local.GetDirtyAsync()).ToList();

    protected override async Task<List<Patient>> MapToCloudBatchAsync(List<Patient> dirtyItems, CancellationToken ct)
    {
        var result = new List<Patient>();
        foreach (var patient in dirtyItems)
        {
            ct.ThrowIfCancellationRequested();
            var cloudUserId = await _identityResolver.ResolveCloudUserIdAsync(patient.UserId, ct);
            if (cloudUserId is null)
            {
                Logger.LogWarning("[{Table}] Cloud User not found for local UserId {UserId} — skipped.", TableName, patient.UserId);
                continue;
            }
            result.Add(new Patient
            {
                UserId = cloudUserId.Value,
                BloodType = patient.BloodType,
                Allergies = patient.Allergies,
                MedicalHistoryNotes = patient.MedicalHistoryNotes,
                Weight = patient.Weight,
                Height = patient.Height,
                BloodPressureSystolic = patient.BloodPressureSystolic,
                BloodPressureDiastolic = patient.BloodPressureDiastolic,
                Cholesterol = patient.Cholesterol,
                Cnp = patient.Cnp,
                CreatedAt = patient.CreatedAt,
                UpdatedAt = patient.UpdatedAt
            });
        }
        return result;
    }

    protected override async Task UpsertToCloudBatchAsync(List<Patient> cloudItems, CancellationToken ct)
    {
        foreach (var patient in cloudItems)
        {
            ct.ThrowIfCancellationRequested();
            var existing = await _cloud!.GetByUserIdAsync(patient.UserId);
            if (existing is not null)
            {
                existing.BloodType = patient.BloodType;
                existing.Allergies = patient.Allergies;
                existing.MedicalHistoryNotes = patient.MedicalHistoryNotes;
                existing.Weight = patient.Weight;
                existing.Height = patient.Height;
                existing.BloodPressureSystolic = patient.BloodPressureSystolic;
                existing.BloodPressureDiastolic = patient.BloodPressureDiastolic;
                existing.Cholesterol = patient.Cholesterol;
                existing.Cnp = patient.Cnp;
                await _cloud.UpdateAsync(existing);
            }
            else
            {
                await _cloud.AddAsync(patient);
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
        var localPatients = (await _local.GetAllAsync()).ToList();
        var scopes = new List<PullScope>();

        foreach (var localPatient in localPatients)
        {
            ct.ThrowIfCancellationRequested();
            var cloudUserId = await _identityResolver.ResolveCloudUserIdAsync(localPatient.UserId, ct);
            if (cloudUserId is null) continue;

            var cloudPatient = await _cloud!.GetByUserIdAsync(cloudUserId.Value);
            if (cloudPatient is null) continue;

            scopes.Add(new PullScope(localPatient.Id, cloudPatient.Id, localPatient));
        }

        return scopes;
    }

    protected override async Task<IReadOnlyList<Patient>> FetchCloudItemsAsync(PullScope scope, CancellationToken ct)
    {
        var cloudPatient = await _cloud!.GetByIdAsync(scope.CloudId);
        return cloudPatient is null ? [] : [cloudPatient];
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
