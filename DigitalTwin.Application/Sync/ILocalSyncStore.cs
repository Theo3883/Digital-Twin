namespace DigitalTwin.Application.Sync;

/// <summary>
/// Local (SQLite) store for temporary sync data.
/// Data is purged after successful sync to keep storage low.
/// </summary>
public interface ILocalSyncStore<T>
{
    Task<IReadOnlyList<T>> GetDirtyAsync();

    Task MarkSyncedAsync(IEnumerable<T> items);

    /// <summary>
    /// Removes synced records older than cutoff to free local storage.
    /// </summary>
    Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc);
}
