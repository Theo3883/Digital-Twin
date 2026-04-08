using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Defines background health data synchronization operations.
/// </summary>
public interface IHealthDataSyncService
{
    /// <summary>
    /// Starts background synchronization and begins live vitals collection when a patient profile exists.
    /// </summary>
    Task StartSyncAsync();

    /// <summary>
    /// Starts live vitals collection for the current patient profile.
    /// </summary>
    Task StartVitalsCollectionAsync();

    /// <summary>
    /// Stops all active sync subscriptions and timers.
    /// </summary>
    void StopSync();

    /// <summary>
    /// Persists a supplied batch of vital-sign readings.
    /// </summary>
    Task SyncBatchAsync(IEnumerable<VitalSign> vitals);

    /// <summary>
    /// Pushes locally cached dirty records to the cloud store.
    /// </summary>
    Task PushToCloudAsync();
}
