using DigitalTwin.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync.Drainers;

/// <summary>
/// Bidirectional sync for <c>EnvironmentReading</c> rows.
///
/// PUSH: dirty local readings → cloud (batch insert).
/// PULL: fetch recent cloud readings (last 30 days, max 200) and add any missing
///       to the local cache. Environment readings are location-based (not patient-scoped)
///       but are bounded by time to keep the local cache small.
/// </summary>
public sealed class EnvironmentReadingDrainer : ITableDrainer
{
    private const int ChunkSize = 50;
    private const int PullLimit = 200;
    private static readonly TimeSpan PurgeOlderThan = TimeSpan.FromDays(30);
    private static readonly TimeSpan PullWindow = TimeSpan.FromDays(30);

    private readonly IEnvironmentReadingRepository _local;
    private readonly IEnvironmentReadingRepository? _cloud;
    private readonly ILogger<EnvironmentReadingDrainer> _logger;

    public int Order => 4;
    public string TableName => "EnvironmentReadings";

    public EnvironmentReadingDrainer(
        IEnvironmentReadingRepository local,
        IEnvironmentReadingRepository? cloud,
        ILogger<EnvironmentReadingDrainer> logger)
    {
        _local = local;
        _cloud = cloud;
        _logger = logger;
    }

    public async Task<int> DrainAsync(CancellationToken ct = default)
    {
        if (_cloud is null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("[{Table}] Cloud repository not configured — skipping.", TableName);
            return 0;
        }

        var pushed = await PushAsync(ct);
        var pulled = await PullAsync(ct);
        return pushed + pulled;
    }

    // ── PUSH ─────────────────────────────────────────────────────────────────

    private async Task<int> PushAsync(CancellationToken ct)
    {
        var dirty = (await _local.GetDirtyAsync()).ToList();
        if (dirty.Count == 0) return 0;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[{Table}] Pushing {Count} dirty rows to cloud.", TableName, dirty.Count);

        foreach (var chunk in dirty.Chunk(ChunkSize))
        {
            ct.ThrowIfCancellationRequested();
            await _cloud!.AddRangeAsync(chunk);
        }

        var maxTs = dirty.Max(r => r.Timestamp);
        await _local.MarkSyncedAsync(maxTs);
        await _local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);
        return dirty.Count;
    }

    // ── PULL ──────────────────────────────────────────────────────────────────
    // Bounded by time window and limit to keep local cache small.

    private async Task<int> PullAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow - PullWindow;
        var cloudReadings = (await _cloud!.GetSinceAsync(since, PullLimit)).ToList();
        if (cloudReadings.Count == 0) return 0;

        int count = 0;
        foreach (var reading in cloudReadings)
        {
            ct.ThrowIfCancellationRequested();
            if (await _local.ExistsAsync(reading.Timestamp)) continue;
            await _local.AddAsync(reading);
            count++;
        }

        if (count > 0 && _logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[{Table}] Pulled {Count} new environment readings from cloud.", TableName, count);

        return count;
    }
}
