using DigitalTwin.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync.Drainers;

/// <summary>
/// Drains dirty <c>EnvironmentReading</c> rows from local SQLite to the cloud database.
/// Environment readings are location-based (not patient-scoped) so <c>MarkSyncedAsync</c>
/// uses a timestamp cutoff rather than a patient-grouped approach.
/// </summary>
public sealed class EnvironmentReadingDrainer : ITableDrainer
{
    private const int ChunkSize = 50;
    private static readonly TimeSpan PurgeOlderThan = TimeSpan.FromDays(30);

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
            _logger.LogDebug("[{Table}] Cloud repository not configured — skipping.", TableName);
            return 0;
        }

        var dirty = (await _local.GetDirtyAsync()).ToList();
        if (dirty.Count == 0) return 0;

        _logger.LogDebug("[{Table}] Draining {Count} dirty rows to cloud.", TableName, dirty.Count);

        // Batch upload — throws on failure → records stay dirty → retry next cycle.
        foreach (var chunk in dirty.Chunk(ChunkSize))
        {
            ct.ThrowIfCancellationRequested();
            await _cloud.AddRangeAsync(chunk);
        }

        // Cloud write succeeded — commit sync.
        var maxTs = dirty.Max(r => r.Timestamp);
        await _local.MarkSyncedAsync(maxTs);
        await _local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);

        return dirty.Count;
    }
}
