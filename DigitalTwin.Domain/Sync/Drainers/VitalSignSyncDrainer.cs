using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Sync;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Domain.Sync.Drainers;

/// <summary>
/// Bidirectional sync for <c>VitalSign</c> rows.
/// PUSH: dirty local vitals → cloud (batch insert, maps local PatientId → cloud PatientId).
/// PULL: for each local patient, fetch recent cloud vitals and add any missing locally.
/// Must run after <see cref="PatientSyncDrainer"/>.
/// </summary>
public sealed class VitalSignSyncDrainer : SyncDrainerBase<VitalSign>
{
    private readonly IVitalSignRepository _local;
    private readonly IVitalSignRepository? _cloud;
    private readonly IPatientRepository _localPatient;
    private readonly ICloudIdentityResolver _identityResolver;

    public override int Order => 3;
    public override string TableName => "VitalSigns";
    protected override bool IsCloudConfigured => _cloud is not null;

    public VitalSignSyncDrainer(
        IVitalSignRepository local,
        IVitalSignRepository? cloud,
        IPatientRepository localPatient,
        ICloudIdentityResolver identityResolver,
        ILogger<VitalSignSyncDrainer> logger) : base(logger)
    {
        _local = local;
        _cloud = cloud;
        _localPatient = localPatient;
        _identityResolver = identityResolver;
    }

    // ── Push hooks ───────────────────────────────────────────────────────────

    protected override async Task<List<VitalSign>> GetDirtyItemsAsync(CancellationToken ct)
        => (await _local.GetDirtyAsync()).ToList();

    protected override async Task<List<VitalSign>> MapToCloudBatchAsync(List<VitalSign> dirtyItems, CancellationToken ct)
    {
        var result = new List<VitalSign>();
        var localToCloud = new Dictionary<Guid, Guid>();

        foreach (var v in dirtyItems)
        {
            ct.ThrowIfCancellationRequested();

            if (!localToCloud.TryGetValue(v.PatientId, out var cloudPatientId))
            {
                cloudPatientId = await ResolveAndCacheCloudPatientIdAsync(v.PatientId, localToCloud, ct);
                if (cloudPatientId == Guid.Empty) continue;
            }

            result.Add(new VitalSign
            {
                PatientId = cloudPatientId,
                Type = v.Type,
                Value = v.Value,
                Unit = v.Unit,
                Source = v.Source,
                Timestamp = v.Timestamp
            });
        }

        return result;
    }

    protected override async Task UpsertToCloudBatchAsync(List<VitalSign> cloudItems, CancellationToken ct)
        => await _cloud!.AddRangeAsync(cloudItems);

    protected override async Task MarkPushedAsSyncedAsync(List<VitalSign> originalDirtyItems, CancellationToken ct)
    {
        foreach (var group in originalDirtyItems.GroupBy(v => v.PatientId))
            await _local.MarkSyncedAsync(group.Key, group.Max(v => v.Timestamp));
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

    protected override async Task<IReadOnlyList<VitalSign>> FetchCloudItemsAsync(PullScope scope, CancellationToken ct)
    {
        var since = DateTime.UtcNow - PullWindow;
        return (await _cloud!.GetByPatientAsync(scope.CloudId, from: since)).ToList();
    }

    protected override async Task<int> MergeCloudItemsToLocalAsync(
        IReadOnlyList<VitalSign> cloudItems, PullScope scope, CancellationToken ct)
    {
        // Load existing local vitals once into a HashSet to avoid N+1 EXISTS queries.
        var since = DateTime.UtcNow - PullWindow;
        var existing = await _local.GetByPatientAsync(scope.LocalId, from: since);
        var existingSet = existing
            .Select(v => (v.Type, v.Timestamp))
            .ToHashSet();

        var toAdd = cloudItems
            .Where(v => !existingSet.Contains((v.Type, v.Timestamp)))
            .Select(v => new VitalSign
            {
                PatientId = scope.LocalId,
                Type = v.Type,
                Value = v.Value,
                Unit = v.Unit,
                Source = v.Source,
                Timestamp = v.Timestamp
            })
            .ToList();

        if (toAdd.Count == 0) return 0;

        // Insert in chunks of ChunkSize (10 000) so each SQLite transaction stays
        // bounded and doesn't block the UI thread for minutes on large pulls.
        foreach (var chunk in toAdd.Chunk(ChunkSize))
            await _local.AddRangeAsync(chunk);

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
