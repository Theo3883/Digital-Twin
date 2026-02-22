namespace DigitalTwin.Application.Sync;

/// <summary>
/// Generic facade that uploads data from local SQLite to cloud PostgreSQL,
/// checks for differences, reconciles them, and purges old local data to keep storage low.
/// </summary>
public interface ISyncFacade<T>
{
    /// <summary>
    /// Uploads dirty records from local to cloud. Returns the batch that was synced.
    /// </summary>
    Task<IReadOnlyList<T>> UploadToCloudAsync();

    /// <summary>
    /// Compares dirty local data with cloud and returns differences (missing in cloud).
    /// </summary>
    Task<SyncDiffResult<T>> DiffAsync();

    /// <summary>
    /// Verifies recently synced batch against cloud and returns any missing.
    /// </summary>
    Task<SyncDiffResult<T>> DiffAsync(IReadOnlyList<T> recentlySynced);

    /// <summary>
    /// Reconciles differences (retries missing, resolves conflicts).
    /// </summary>
    Task ReconcileAsync(SyncDiffResult<T> diff);

    /// <summary>
    /// Removes old synced records from local to keep storage low.
    /// </summary>
    Task PurgeOldLocalDataAsync(TimeSpan olderThan);

    /// <summary>
    /// Full sync: upload, diff, reconcile, purge.
    /// </summary>
    Task SyncAsync(TimeSpan purgeOlderThan);
}
