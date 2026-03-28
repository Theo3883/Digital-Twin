namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Tracks the current Blazor route so native pull-to-refresh can target the active page.
/// </summary>
public interface IAppRouteState
{
    string CurrentPath { get; }

    void SetCurrentPath(string absolutePath);
}
