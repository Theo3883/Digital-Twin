using DigitalTwin.Application.Interfaces;

namespace DigitalTwin.Integrations.Sync;

public class ConnectivityMonitor : IDisposable
{
    private readonly IHealthDataSyncService _syncService;

    public ConnectivityMonitor(IHealthDataSyncService syncService)
    {
        _syncService = syncService;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            try
            {
                await _syncService.PushToCloudAsync();
            }
            catch
            {
                // Sync failed; will retry on next connectivity change
            }
        }
    }

    public void Dispose()
    {
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        GC.SuppressFinalize(this);
    }
}
