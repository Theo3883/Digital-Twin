using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Interfaces;

public interface IHealthDataSyncService
{
    /// <summary>Starts the live vitals subscription and background timers.</summary>
    Task StartSyncAsync();

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
