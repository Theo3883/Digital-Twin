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
    private readonly IVitalSignSyncClient? _syncClient;
    private readonly IPatientRepository _localPatient;

    public override int Order => 3;
    public override string TableName => "VitalSigns";
    protected override bool IsCloudConfigured => _syncClient is not null;

    public VitalSignSyncDrainer(
        IVitalSignRepository local,
        IVitalSignSyncClient? syncClient,
        IPatientRepository localPatient,
        ILogger<VitalSignSyncDrainer> logger) : base(logger)
    {
        _local = local;
        _syncClient = syncClient;
        _localPatient = localPatient;
    }

    // ── Push hooks ───────────────────────────────────────────────────────────

    protected override async Task<List<VitalSign>> GetDirtyItemsAsync(CancellationToken ct)
        => (await _local.GetDirtyAsync()).ToList();

    protected override Task<List<VitalSign>> MapToCloudBatchAsync(List<VitalSign> dirtyItems, CancellationToken ct)
        => Task.FromResult(dirtyItems);

    protected override async Task UpsertToCloudBatchAsync(List<VitalSign> cloudItems, CancellationToken ct)
    {
        if (cloudItems.Count == 0) return;

        var success = await _syncClient!.PushVitalSignsAsync(cloudItems, ct);
        if (!success)
        {
            Logger.LogError("[VitalSignSyncDrainer] Failed to sync {Count} vitals", cloudItems.Count);
            throw new InvalidOperationException($"Failed to sync {cloudItems.Count} vitals");
        }
    }

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
        if (localPatientIds.Count == 0) return [];

        // For mobile app, assume single patient profile
        var localPatientId = localPatientIds[0];
        return [new PullScope(localPatientId, localPatientId)];
    }

    protected override async Task<IReadOnlyList<VitalSign>> FetchCloudItemsAsync(PullScope scope, CancellationToken ct)
    {
        try
        {
            var since = DateTime.UtcNow - PullWindow;
            var cloudVitals = await _syncClient!.PullVitalSignsAsync(scope.LocalId, since, DateTime.UtcNow, ct);
            return cloudVitals.ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[VitalSignSyncDrainer] Failed to fetch vitals from cloud");
            return [];
        }
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

}
