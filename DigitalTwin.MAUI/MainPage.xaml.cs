using DigitalTwin.Application.Interfaces;

namespace DigitalTwin;

public partial class MainPage : ContentPage
{
    private readonly IAppRouteState _route;
    private readonly IPullRefreshCoordinator _pullRefresh;

    public MainPage(IAppRouteState route, IPullRefreshCoordinator pullRefresh)
    {
        InitializeComponent();
        _route = route;
        _pullRefresh = pullRefresh;
    }

    private async void MainRefresh_OnRefreshing(object? sender, EventArgs e)
    {
        try
        {
            var path = _route.CurrentPath.TrimEnd('/');
            if (string.Equals(path, "/environment", StringComparison.OrdinalIgnoreCase))
                await _pullRefresh.EnvironmentRefreshAsync().ConfigureAwait(true);
            else if (path is "" or "/" or "/home")
                await _pullRefresh.HomeRefreshAsync().ConfigureAwait(true);
            else if (string.Equals(path, "/medications", StringComparison.OrdinalIgnoreCase))
                await _pullRefresh.MedicationsRefreshAsync().ConfigureAwait(true);
        }
        catch
        {
            // Swallow — Blazor pages show their own errors when needed
        }
        finally
        {
            MainRefresh.IsRefreshing = false;
        }
    }
}
