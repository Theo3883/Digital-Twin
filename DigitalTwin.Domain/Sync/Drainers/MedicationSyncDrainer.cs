using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Sync;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Domain.Sync.Drainers;

/// <summary>
/// Bidirectional sync for <c>Medication</c> rows.
/// PUSH: dirty local medications → cloud (maps local PatientId → cloud PatientId).
/// PULL: for each local patient, fetch cloud medications and add any missing locally.
/// Must run after <see cref="PatientSyncDrainer"/>.
/// </summary>
public sealed class MedicationSyncDrainer : SyncDrainerBase<Medication>
{
    private readonly IMedicationRepository _local;
    private readonly IMedicationRepository? _cloud;
    private readonly IPatientRepository _localPatient;
    private readonly ICloudIdentityResolver _identityResolver;

    public override int Order => 6;
    public override string TableName => "Medications";
    protected override bool IsCloudConfigured => _cloud is not null;

    public MedicationSyncDrainer(
        IMedicationRepository local,
        IMedicationRepository? cloud,
        IPatientRepository localPatient,
        ICloudIdentityResolver identityResolver,
        ILogger<MedicationSyncDrainer> logger) : base(logger)
    {
        _local = local;
        _cloud = cloud;
        _localPatient = localPatient;
        _identityResolver = identityResolver;
    }

    // ── Push hooks ───────────────────────────────────────────────────────────

    protected override async Task<List<Medication>> GetDirtyItemsAsync(CancellationToken ct)
        => (await _local.GetDirtyAsync()).ToList();

    protected override async Task<List<Medication>> MapToCloudBatchAsync(List<Medication> dirtyItems, CancellationToken ct)
    {
        var result = new List<Medication>();
        var localToCloud = new Dictionary<Guid, Guid>();

        foreach (var med in dirtyItems)
        {
            ct.ThrowIfCancellationRequested();

            if (!localToCloud.TryGetValue(med.PatientId, out var cloudPatientId))
            {
                cloudPatientId = await ResolveAndCacheCloudPatientIdAsync(med.PatientId, localToCloud, ct);
                if (cloudPatientId == Guid.Empty) continue;
            }

            result.Add(new Medication
            {
                Id = med.Id,
                PatientId = cloudPatientId,
                Name = med.Name,
                Dosage = med.Dosage,
                Frequency = med.Frequency,
                Route = med.Route,
                RxCui = med.RxCui,
                Instructions = med.Instructions,
                Reason = med.Reason,
                PrescribedByUserId = med.PrescribedByUserId,
                StartDate = med.StartDate,
                EndDate = med.EndDate,
                Status = med.Status,
                DiscontinuedReason = med.DiscontinuedReason,
                AddedByRole = med.AddedByRole,
                CreatedAt = med.CreatedAt,
                UpdatedAt = med.UpdatedAt
            });
        }

        return result;
    }

    protected override async Task UpsertToCloudBatchAsync(List<Medication> cloudItems, CancellationToken ct)
        => await _cloud!.UpsertRangeAsync(cloudItems);

    protected override async Task MarkPushedAsSyncedAsync(List<Medication> originalDirtyItems, CancellationToken ct)
    {
        foreach (var group in originalDirtyItems.GroupBy(m => m.PatientId))
            await _local.MarkSyncedAsync(group.Key);
    }

    protected override async Task PurgeSyncedAsync(CancellationToken ct)
        => await _local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);

    // ── Pull hooks ───────────────────────────────────────────────────────────

    protected override async Task<IReadOnlyList<PullScope>> GetPullScopesAsync(CancellationToken ct)
    {
        var localPatientIds = (await _localPatient.GetAllAsync()).Select(p => p.Id).ToList();
        var scopes = new List<PullScope>();

        foreach (var localPatientId in localPatientIds)
        {
            ct.ThrowIfCancellationRequested();
            var cloudPatientId = await _identityResolver.ResolveCloudPatientIdAsync(localPatientId, ct);
            if (cloudPatientId is null) continue;
            scopes.Add(new PullScope(localPatientId, cloudPatientId.Value));
        }

        return scopes;
    }

    protected override async Task<IReadOnlyList<Medication>> FetchCloudItemsAsync(PullScope scope, CancellationToken ct)
        => (await _cloud!.GetByPatientAsync(scope.CloudId)).ToList();

    protected override async Task<int> MergeCloudItemsToLocalAsync(
        IReadOnlyList<Medication> cloudItems, PullScope scope, CancellationToken ct)
    {
        // Bulk fetch all local medications for the patient once to avoid N+1 EXISTS queries.
        var existing = await _local.GetByPatientAsync(scope.LocalId);
        var existingIds = existing.Select(m => m.Id).ToHashSet();

        var toAdd = cloudItems
            .Where(m => !existingIds.Contains(m.Id))
            .Select(m => new Medication
            {
                Id = m.Id,
                PatientId = scope.LocalId,
                Name = m.Name,
                Dosage = m.Dosage,
                Frequency = m.Frequency,
                Route = m.Route,
                RxCui = m.RxCui,
                Instructions = m.Instructions,
                Reason = m.Reason,
                PrescribedByUserId = m.PrescribedByUserId,
                StartDate = m.StartDate,
                EndDate = m.EndDate,
                Status = m.Status,
                DiscontinuedReason = m.DiscontinuedReason,
                AddedByRole = m.AddedByRole,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            })
            .ToList();

        if (toAdd.Count == 0) return 0;

        await _local.AddRangeAsync(toAdd);
        return toAdd.Count;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<Guid> ResolveAndCacheCloudPatientIdAsync(
        Guid localPatientId, Dictionary<Guid, Guid> cache, CancellationToken ct)
    {
        var cloudPatientId = await _identityResolver.ResolveCloudPatientIdAsync(localPatientId, ct);
        if (cloudPatientId is null)
        {
            Logger.LogWarning("[{Table}] Cloud Patient not found for local PatientId {Id} — skipped.", TableName, localPatientId);
            return Guid.Empty;
        }
        cache[localPatientId] = cloudPatientId.Value;
        return cloudPatientId.Value;
    }
}
