namespace DigitalTwin.Mobile.Domain.Interfaces;

/// <summary>
/// Service for persisting and retrieving sync checkpoint data.
/// Tracks last sync timestamp for each entity type to enable incremental sync.
/// </summary>
public interface ISyncStateService
{
    /// <summary>
    /// Get the last successful sync timestamp for an entity type
    /// </summary>
    Task<DateTime?> GetLastSyncTimeAsync(string entityType);

    /// <summary>
    /// Update the last successful sync timestamp for an entity type
    /// </summary>
    Task SetLastSyncTimeAsync(string entityType, DateTime syncTime);

    /// <summary>
    /// Reset all sync checkpoints (used for full resync)
    /// </summary>
    Task ResetAllCheckpointsAsync();

    /// <summary>
    /// Get all sync state for diagnostics
    /// </summary>
    Task<Dictionary<string, DateTime?>> GetAllSyncStatesAsync();
}
