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

    public SyncFacade(ILocalSyncStore<T> local, ICloudSyncStore<T> cloud)
    {
        _local = local;
        _cloud = cloud;
    }

    public async Task<IReadOnlyList<T>> UploadToCloudAsync()
    {
        var dirty = await _local.GetDirtyAsync();
        if (dirty.Count == 0) return [];

        var synced = new List<T>();
        foreach (var item in dirty)
        {
            try
            {
                await _cloud.AddAsync(item);
                synced.Add(item);
            }
            catch
            {
                break;
            }
        }

        if (synced.Count > 0)
            await _local.MarkSyncedAsync(synced);

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

        return new SyncDiffResult<T>
        {
            MissingInCloud = missingInCloud,
            Conflicts = []
        };
    }

    public async Task ReconcileAsync(SyncDiffResult<T> diff)
    {
        if (!diff.HasIssues) return;

        foreach (var item in diff.MissingInCloud)
        {
            try
            {
                await _cloud.AddAsync(item);
                await _local.MarkSyncedAsync([item]);
            }
            catch
            {
                // Retry on next sync
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
            catch
            {
                // Retry on next sync
            }
        }
    }

    public async Task PurgeOldLocalDataAsync(TimeSpan olderThan)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        await _local.PurgeSyncedOlderThanAsync(cutoff);
    }

    public async Task SyncAsync(TimeSpan purgeOlderThan)
    {
        var synced = await UploadToCloudAsync();
        var diff = synced.Count > 0 ? await DiffAsync(synced) : new SyncDiffResult<T>();
        await ReconcileAsync(diff);
        await PurgeOldLocalDataAsync(purgeOlderThan);
    }
}
