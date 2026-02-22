using DigitalTwin.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalTwin.Integrations.Sync;

public class ConnectivityMonitor : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ConnectivityMonitor(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess != NetworkAccess.Internet)
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IHealthDataSyncService>();
            await syncService.PushToCloudAsync();
        }
        catch
        {
            // Sync failed; will retry on next connectivity change
        }
    }

    public void Dispose()
    {
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        GC.SuppressFinalize(this);
    }
}
