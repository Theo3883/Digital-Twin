using DigitalTwin.Application.Interfaces;

namespace DigitalTwin.Services;

public sealed class PullRefreshCoordinator : IPullRefreshCoordinator
{
    private Func<Task>? _environment;
    private Func<Task>? _home;
    private Func<Task>? _medications;

    public void RegisterEnvironment(Func<Task> refresh) => _environment = refresh;

    public void UnregisterEnvironment() => _environment = null;

    public void RegisterHome(Func<Task> refresh) => _home = refresh;

    public void UnregisterHome() => _home = null;

    public void RegisterMedications(Func<Task> refresh) => _medications = refresh;

    public void UnregisterMedications() => _medications = null;

    public Task EnvironmentRefreshAsync() => _environment?.Invoke() ?? Task.CompletedTask;

    public Task HomeRefreshAsync() => _home?.Invoke() ?? Task.CompletedTask;

    public Task MedicationsRefreshAsync() => _medications?.Invoke() ?? Task.CompletedTask;
}
