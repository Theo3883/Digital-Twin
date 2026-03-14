using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Sync;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Domain.Sync.Drainers;

/// <summary>
/// Bidirectional sync for <c>SleepSession</c> rows.
/// PUSH: dirty local sessions → cloud (batch insert, maps local PatientId → cloud PatientId).
/// PULL: for each local patient, fetch cloud sleep sessions and add any missing locally.
/// Must run after <see cref="PatientSyncDrainer"/>.
/// </summary>
public sealed class SleepSessionSyncDrainer : SyncDrainerBase<SleepSession>
{
    private readonly ISleepSessionRepository _local;
    private readonly ISleepSessionRepository? _cloud;
    private readonly IPatientRepository _localPatient;
    private readonly ICloudIdentityResolver _identityResolver;

    public override int Order => 5;
    public override string TableName => "SleepSessions";
    protected override TimeSpan PurgeOlderThan => TimeSpan.FromDays(30);
    protected override TimeSpan PullWindow => TimeSpan.FromDays(30);
    protected override bool IsCloudConfigured => _cloud is not null;

    public SleepSessionSyncDrainer(
        ISleepSessionRepository local,
        ISleepSessionRepository? cloud,
        IPatientRepository localPatient,
        ICloudIdentityResolver identityResolver,
        ILogger<SleepSessionSyncDrainer> logger) : base(logger)
    {
        _local = local;
        _cloud = cloud;
        _localPatient = localPatient;
        _identityResolver = identityResolver;
    }

    // ── Push hooks ───────────────────────────────────────────────────────────

    protected override async Task<List<SleepSession>> GetDirtyItemsAsync(CancellationToken ct)
        => (await _local.GetDirtyAsync()).ToList();

    protected override async Task<List<SleepSession>> MapToCloudBatchAsync(List<SleepSession> dirtyItems, CancellationToken ct)
    {
        var result = new List<SleepSession>();
        var localToCloud = new Dictionary<Guid, Guid>();

        foreach (var s in dirtyItems)
        {
            ct.ThrowIfCancellationRequested();

            if (!localToCloud.TryGetValue(s.PatientId, out var cloudPatientId))
            {
                cloudPatientId = await ResolveAndCacheCloudPatientIdAsync(s.PatientId, localToCloud, ct);
                if (cloudPatientId == Guid.Empty) continue;
            }

            result.Add(new SleepSession
            {
                PatientId = cloudPatientId,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                DurationMinutes = s.DurationMinutes,
                QualityScore = s.QualityScore
            });
        }

        return result;
    }

    protected override async Task UpsertToCloudBatchAsync(List<SleepSession> cloudItems, CancellationToken ct)
        => await _cloud!.AddRangeAsync(cloudItems);

    protected override async Task MarkPushedAsSyncedAsync(List<SleepSession> originalDirtyItems, CancellationToken ct)
    {
        foreach (var group in originalDirtyItems.GroupBy(s => s.PatientId))
            await _local.MarkSyncedAsync(group.Key, group.Max(s => s.StartTime));
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

    protected override async Task<IReadOnlyList<SleepSession>> FetchCloudItemsAsync(PullScope scope, CancellationToken ct)
    {
        var since = DateTime.UtcNow - PullWindow;
        return (await _cloud!.GetByPatientAsync(scope.CloudId, from: since)).ToList();
    }

    protected override async Task<int> MergeCloudItemsToLocalAsync(
        IReadOnlyList<SleepSession> cloudItems, PullScope scope, CancellationToken ct)
    {
        // Bulk fetch all local sessions in the pull window once to avoid N+1 EXISTS queries.
        var since = DateTime.UtcNow - PullWindow;
        var existing = await _local.GetByPatientAsync(scope.LocalId, from: since);
        var existingSet = existing.Select(s => s.StartTime).ToHashSet();

        var toAdd = cloudItems
            .Where(s => !existingSet.Contains(s.StartTime))
            .Select(s => new SleepSession
            {
                PatientId = scope.LocalId,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                DurationMinutes = s.DurationMinutes,
                QualityScore = s.QualityScore
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
