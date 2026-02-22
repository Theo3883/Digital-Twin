namespace DigitalTwin.Application.Sync;

public sealed class SyncDiffResult<T>
{
    public IReadOnlyList<T> MissingInCloud { get; init; } = [];
    public IReadOnlyList<T> Conflicts { get; init; } = [];
    public bool HasIssues => MissingInCloud.Count > 0 || Conflicts.Count > 0;
}
