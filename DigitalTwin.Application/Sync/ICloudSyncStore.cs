namespace DigitalTwin.Application.Sync;

/// <summary>
/// Cloud (PostgreSQL) store - persistent sync target.
/// </summary>
public interface ICloudSyncStore<T>
{
    Task AddAsync(T item);

    /// <summary>
    /// Checks if an equivalent record exists in cloud (for diff/reconcile).
    /// </summary>
    Task<bool> ExistsAsync(T item);
}
