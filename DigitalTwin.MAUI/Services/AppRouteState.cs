using DigitalTwin.Application.Interfaces;

namespace DigitalTwin.Services;

public sealed class AppRouteState : IAppRouteState
{
    public string CurrentPath { get; private set; } = "/";

    public void SetCurrentPath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            CurrentPath = "/";
            return;
        }

        var p = absolutePath.TrimEnd('/');
        CurrentPath = p.Length == 0 ? "/" : p;
    }
}
