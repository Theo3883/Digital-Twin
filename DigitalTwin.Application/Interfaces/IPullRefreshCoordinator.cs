namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Connects native pull-to-refresh to Blazor page handlers.
/// </summary>
public interface IPullRefreshCoordinator
{
    void RegisterEnvironment(Func<Task> refresh);

    void UnregisterEnvironment();

    void RegisterHome(Func<Task> refresh);

    void UnregisterHome();

    Task EnvironmentRefreshAsync();

    Task HomeRefreshAsync();
}
