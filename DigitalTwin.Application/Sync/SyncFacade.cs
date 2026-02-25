using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync;

/// <summary>
/// Generic facade that uploads data from local SQLite to cloud PostgreSQL,
/// checks for differences, reconciles them, and purges old local data.
/// Local storage stays low by purging synced records.
/// </summary>
public class SyncFacade<T> : ISyncFacade<T>
{
    private readonly ILocalSyncStore<T> _local;
    private readonly ICloudSyncStore<T> _cloud;
    private readonly ILogger<SyncFacade<T>> _logger;

    public SyncFacade(ILocalSyncStore<T> local, ICloudSyncStore<T> cloud, ILogger<SyncFacade<T>> logger)
    {
        _local = local;
        _cloud = cloud;
        _logger = logger;
    }

    public async Task<IReadOnlyList<T>> UploadToCloudAsync()
    {
        var dirty = await _local.GetDirtyAsync();
        _logger.LogInformation("[Sync<{Type}>] UploadToCloud: {Count} dirty items found.", typeof(T).Name, dirty.Count);

        if (dirty.Count == 0) return [];

        var synced = new List<T>();
        foreach (var item in dirty)
        {
            try
            {
                await _cloud.AddAsync(item);
                synced.Add(item);
                _logger.LogInformation("[Sync<{Type}>] Uploaded item to cloud successfully.", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Sync<{Type}>] Failed to upload item to cloud. Stopping batch.", typeof(T).Name);
                break;
            }
        }

        if (synced.Count > 0)
        {
            await _local.MarkSyncedAsync(synced);
            _logger.LogInformation("[Sync<{Type}>] Marked {Count} items as synced in local DB.", typeof(T).Name, synced.Count);
        }

        return synced;
    }

    public async Task<SyncDiffResult<T>> DiffAsync()
    {
        var dirty = await _local.GetDirtyAsync();
        var missingInCloud = new List<T>();

        foreach (var item in dirty)
        {
            try
            {
                if (!await _cloud.ExistsAsync(item))
                    missingInCloud.Add(item);
            }
            catch
            {
                missingInCloud.Add(item);
            }
        }

        _logger.LogInformation("[Sync<{Type}>] Diff: {Missing} missing in cloud out of {Total} dirty.",
            typeof(T).Name, missingInCloud.Count, dirty.Count);

        return new SyncDiffResult<T>
        {
            MissingInCloud = missingInCloud,
            Conflicts = []
        };
    }

    /// <summary>
    /// Diffs a batch of recently synced items against cloud (verification).
    /// </summary>
    public async Task<SyncDiffResult<T>> DiffAsync(IReadOnlyList<T> recentlySynced)
    {
        var missingInCloud = new List<T>();
        foreach (var item in recentlySynced)
        {
            try
            {
                if (!await _cloud.ExistsAsync(item))
                    missingInCloud.Add(item);
            }
            catch
            {
                missingInCloud.Add(item);
            }
        }

        _logger.LogInformation("[Sync<{Type}>] Verification diff: {Missing} missing out of {Total} recently synced.",
            typeof(T).Name, missingInCloud.Count, recentlySynced.Count);

        return new SyncDiffResult<T>
        {
            MissingInCloud = missingInCloud,
            Conflicts = []
        };
    }

    public async Task ReconcileAsync(SyncDiffResult<T> diff)
    {
        if (!diff.HasIssues)
        {
            _logger.LogInformation("[Sync<{Type}>] No reconciliation needed.", typeof(T).Name);
            return;
        }

        _logger.LogInformation("[Sync<{Type}>] Reconciling {Missing} missing, {Conflicts} conflicts.",
            typeof(T).Name, diff.MissingInCloud.Count, diff.Conflicts.Count);

        foreach (var item in diff.MissingInCloud)
        {
            try
            {
                await _cloud.AddAsync(item);
                await _local.MarkSyncedAsync([item]);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Sync<{Type}>] Reconcile failed for missing item. Will retry next sync.", typeof(T).Name);
            }
        }

        // Conflicts: last-write-wins - re-push to cloud
        foreach (var item in diff.Conflicts)
        {
            try
            {
                await _cloud.AddAsync(item);
                await _local.MarkSyncedAsync([item]);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Sync<{Type}>] Reconcile failed for conflict item. Will retry next sync.", typeof(T).Name);
            }
        }
    }

    public async Task PurgeOldLocalDataAsync(TimeSpan olderThan)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        await _local.PurgeSyncedOlderThanAsync(cutoff);
        _logger.LogInformation("[Sync<{Type}>] Purged synced local data older than {Cutoff}.", typeof(T).Name, cutoff);
    }

    public async Task SyncAsync(TimeSpan purgeOlderThan)
    {
        _logger.LogInformation("[Sync<{Type}>] Starting full sync cycle...", typeof(T).Name);
        var synced = await UploadToCloudAsync();
        var diff = synced.Count > 0 ? await DiffAsync(synced) : new SyncDiffResult<T>();
        await ReconcileAsync(diff);
        await PurgeOldLocalDataAsync(purgeOlderThan);
        _logger.LogInformation("[Sync<{Type}>] Sync cycle complete.", typeof(T).Name);
    }
}
