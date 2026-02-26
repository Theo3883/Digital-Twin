using DigitalTwin.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync.Drainers;

/// <summary>
/// Drains dirty <c>VitalSign</c> rows from local SQLite to the cloud database.
/// Cloud upload uses <c>AddRangeAsync</c> which wraps in a single <c>SaveChangesAsync</c>
/// transaction — if any row fails the whole batch is rolled back and stays dirty.
/// </summary>
public sealed class VitalSignDrainer : ITableDrainer
{
    private const int ChunkSize = 100;
    private static readonly TimeSpan PurgeOlderThan = TimeSpan.FromDays(7);

    private readonly IVitalSignRepository _local;
    private readonly IVitalSignRepository? _cloud;
    private readonly ILogger<VitalSignDrainer> _logger;

    public string TableName => "VitalSigns";

    public VitalSignDrainer(
        IVitalSignRepository local,
        IVitalSignRepository? cloud,
        ILogger<VitalSignDrainer> logger)
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

        _logger.LogInformation("[{Table}] Draining {Count} dirty rows to cloud.", TableName, dirty.Count);

        // Batch upload. Each AddRangeAsync call wraps SaveChangesAsync = one DB transaction.
        // If it throws here, nothing below runs → records stay dirty → retry next cycle.
        foreach (var chunk in dirty.Chunk(ChunkSize))
        {
            ct.ThrowIfCancellationRequested();
            await _cloud.AddRangeAsync(chunk);
        }

        // Cloud write succeeded — commit sync in local DB.
        foreach (var group in dirty.GroupBy(v => v.PatientId))
            await _local.MarkSyncedAsync(group.Key, group.Max(v => v.Timestamp));

        await _local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);

        return dirty.Count;
    }
}
