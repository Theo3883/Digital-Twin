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

    void RegisterMedications(Func<Task> refresh);

    void UnregisterMedications();

    Task EnvironmentRefreshAsync();

    Task HomeRefreshAsync();

    Task MedicationsRefreshAsync();
}
