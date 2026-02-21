using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Interfaces;

public interface IHealthDataSyncService
{
    Task StartSyncAsync();
    void StopSync();
    Task SyncBatchAsync(IEnumerable<VitalSign> vitals);
    Task PushToCloudAsync();
}
