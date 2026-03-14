using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Domain.Sync.Drainers;

/// <summary>
/// Bidirectional sync for <c>EnvironmentReading</c> rows.
/// PUSH: dirty local readings → cloud (batch insert).
/// PULL: fetch recent cloud readings (last 30 days, max 200) and add any missing locally.
/// Environment readings are location-based (not patient-scoped).
/// </summary>
public sealed class EnvironmentReadingSyncDrainer : SyncDrainerBase<EnvironmentReading>
{
    private const int PullLimit = 200;

    private readonly IEnvironmentReadingRepository _local;
    private readonly IEnvironmentReadingRepository? _cloud;

    public override int Order => 4;
    public override string TableName => "EnvironmentReadings";
    protected override TimeSpan PurgeOlderThan => TimeSpan.FromDays(30);
    protected override TimeSpan PullWindow => TimeSpan.FromDays(30);
    protected override bool IsCloudConfigured => _cloud is not null;

    public EnvironmentReadingSyncDrainer(
        IEnvironmentReadingRepository local,
        IEnvironmentReadingRepository? cloud,
        ILogger<EnvironmentReadingSyncDrainer> logger) : base(logger)
    {
        _local = local;
        _cloud = cloud;
    }

    // ── Push hooks ───────────────────────────────────────────────────────────

    protected override async Task<List<EnvironmentReading>> GetDirtyItemsAsync(CancellationToken ct)
        => (await _local.GetDirtyAsync()).ToList();

    protected override Task<List<EnvironmentReading>> MapToCloudBatchAsync(
        List<EnvironmentReading> dirtyItems, CancellationToken ct)
        => Task.FromResult(dirtyItems); // No ID remapping needed — location-based entity.

    protected override async Task UpsertToCloudBatchAsync(List<EnvironmentReading> cloudItems, CancellationToken ct)
        => await _cloud!.AddRangeAsync(cloudItems);

    protected override async Task MarkPushedAsSyncedAsync(List<EnvironmentReading> originalDirtyItems, CancellationToken ct)
    {
        var maxTs = originalDirtyItems.Max(r => r.Timestamp);
        await _local.MarkSyncedAsync(maxTs);
    }

    protected override async Task PurgeSyncedAsync(CancellationToken ct)
        => await _local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);

    // ── Pull hooks ───────────────────────────────────────────────────────────

    protected override Task<IReadOnlyList<PullScope>> GetPullScopesAsync(CancellationToken ct)
    {
        // Single sentinel scope — environment readings are global, not per-entity.
        IReadOnlyList<PullScope> scopes = [new PullScope(Guid.Empty, Guid.Empty)];
        return Task.FromResult(scopes);
    }

    protected override async Task<IReadOnlyList<EnvironmentReading>> FetchCloudItemsAsync(PullScope scope, CancellationToken ct)
    {
        var since = DateTime.UtcNow - PullWindow;
        return (await _cloud!.GetSinceAsync(since, PullLimit)).ToList();
    }

    protected override async Task<int> MergeCloudItemsToLocalAsync(
        IReadOnlyList<EnvironmentReading> cloudItems, PullScope scope, CancellationToken ct)
    {
        // Bulk fetch all local readings in the pull window once to avoid N+1 EXISTS queries.
        var since = DateTime.UtcNow - PullWindow;
        var existing = await _local.GetSinceAsync(since, int.MaxValue);
        var existingTimestamps = existing.Select(r => r.Timestamp).ToHashSet();

        var toAdd = cloudItems
            .Where(r => !existingTimestamps.Contains(r.Timestamp))
            .ToList();

        if (toAdd.Count == 0) return 0;

        await _local.AddRangeAsync(toAdd);
        return toAdd.Count;
    }
}
