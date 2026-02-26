using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Interfaces;

public interface IHealthDataSyncService
{
    /// <summary>
    /// Starts the drain timer (always) and optionally the live vitals subscription
    /// if a Patient profile exists for the current user.
    /// </summary>
    Task StartSyncAsync();

    /// <summary>
    /// Starts vitals collection after a Patient profile has been created.
    /// Call this from the Profile page after CreatePatientProfileAsync succeeds.
    /// Does nothing if vitals are already being collected.
    /// </summary>
    Task StartVitalsCollectionAsync();

    /// <summary>Stops the subscription and timers. Safe to call multiple times.</summary>
    void StopSync();

    /// <summary>Manually push a batch of vitals (e.g. from iOS background fetch).</summary>
    Task SyncBatchAsync(IEnumerable<VitalSign> vitals);

    /// <summary> 
    /// Drains any dirty records from the local SQLite cache to the cloud DB.
    /// Called automatically by the drain timer and by ConnectivityMonitor on reconnect.
    /// </summary>
    Task PushToCloudAsync();
}
