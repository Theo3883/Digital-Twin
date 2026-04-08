using DigitalTwin.Domain.Interfaces.Sync;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Domain.Sync;

/// <summary>
/// Template Method base class for bidirectional sync drainers.
///
/// Sealed algorithms:
///   PUSH — get dirty → map → chunk → upsert to cloud → mark synced → purge.
///   PULL — get scopes → for each scope: fetch cloud → merge locally.
///
/// Subclasses override abstract hooks for entity-specific logic.
/// </summary>
public abstract class SyncDrainerBase<TModel> : ISyncDrainer
{
    protected readonly ILogger Logger;

    protected SyncDrainerBase(ILogger logger) => Logger = logger;

    // ── ISyncDrainer contract ────────────────────────────────────────────────

    public abstract int Order { get; }
    public abstract string TableName { get; }

    // ── Configurable properties (override per entity) ────────────────────────

    protected virtual TimeSpan PurgeOlderThan => TimeSpan.FromDays(7);
    protected virtual int ChunkSize => 10000;
    protected virtual TimeSpan PullWindow => TimeSpan.FromDays(7);

    // ── Abstract hooks: cloud check ──────────────────────────────────────────

    protected abstract bool IsCloudConfigured { get; }

    // ── Abstract hooks: PUSH ─────────────────────────────────────────────────

    protected abstract Task<List<TModel>> GetDirtyItemsAsync(CancellationToken ct);
    protected abstract Task<List<TModel>> MapToCloudBatchAsync(List<TModel> dirtyItems, CancellationToken ct);
    protected abstract Task UpsertToCloudBatchAsync(List<TModel> cloudItems, CancellationToken ct);
    protected abstract Task MarkPushedAsSyncedAsync(List<TModel> originalDirtyItems, CancellationToken ct);
    protected abstract Task PurgeSyncedAsync(CancellationToken ct);

    // ── Abstract hooks: PULL ─────────────────────────────────────────────────

    /// <summary>
    /// Lightweight carrier for local ↔ cloud identity pairs during pull.
    /// <paramref name="Context"/> can carry extra data (e.g. email, local entity ref).
    /// </summary>
    protected record PullScope(Guid LocalId, Guid CloudId, object? Context = null);

    protected abstract Task<IReadOnlyList<PullScope>> GetPullScopesAsync(CancellationToken ct);
    protected abstract Task<IReadOnlyList<TModel>> FetchCloudItemsAsync(PullScope scope, CancellationToken ct);
    protected abstract Task<int> MergeCloudItemsToLocalAsync(IReadOnlyList<TModel> cloudItems, PullScope scope, CancellationToken ct);

    // ── Sealed entry point ───────────────────────────────────────────────────

    public async Task<int> DrainAsync(CancellationToken ct = default)
    {
        if (!IsCloudConfigured)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
                Logger.LogDebug("[{Table}] Cloud repository not configured — skipping.", TableName);
            return 0;
        }

        var pushed = await PushAsync(ct);
        var pulled = await PullAsync(ct);
        return pushed + pulled;
    }

    // ── Push template (sealed algorithm) ─────────────────────────────────────

    private async Task<int> PushAsync(CancellationToken ct)
    {
        var dirty = await GetDirtyItemsAsync(ct);
        if (dirty.Count == 0) return 0;

        if (Logger.IsEnabled(LogLevel.Debug))
            Logger.LogDebug("[{Table}] Pushing {Count} dirty rows to cloud.", TableName, dirty.Count);

        var mapped = await MapToCloudBatchAsync(dirty, ct);
        if (mapped.Count == 0)
        {
            Logger.LogWarning("[{Table}] No items could be mapped to cloud — skipping (will retry).", TableName);
            return 0;
        }

        foreach (var chunk in mapped.Chunk(ChunkSize))
        {
            ct.ThrowIfCancellationRequested();
            await UpsertToCloudBatchAsync(chunk.ToList(), ct);
        }

        await MarkPushedAsSyncedAsync(dirty, ct);
        await PurgeSyncedAsync(ct);
        return dirty.Count;
    }

    // ── Pull template (sealed algorithm) ─────────────────────────────────────

    private async Task<int> PullAsync(CancellationToken ct)
    {
        var scopes = await GetPullScopesAsync(ct);
        if (scopes.Count == 0) return 0;

        var total = 0;

        foreach (var scope in scopes)
        {
            ct.ThrowIfCancellationRequested();

            var cloudItems = await FetchCloudItemsAsync(scope, ct);
            if (cloudItems.Count == 0) continue;

            total += await MergeCloudItemsToLocalAsync(cloudItems, scope, ct);
        }

        if (total > 0 && Logger.IsEnabled(LogLevel.Debug))
            Logger.LogDebug("[{Table}] Pulled {Count} items from cloud.", TableName, total);

        return total;
    }

    // ── Protected merge helpers ──────────────────────────────────────────────

    /// <summary>
    /// Dedup-and-add merge strategy: for each cloud item, check if it exists locally;
    /// if not, remap IDs and add it. Returns the number of new items added.
    /// </summary>
    protected static async Task<int> DeduplicateAndAddAsync(
        IReadOnlyList<TModel> cloudItems,
        Func<TModel, Task<bool>> existsAsync,
        Func<TModel, TModel> remapToLocal,
        Func<TModel, Task> addAsync,
        CancellationToken ct)
    {
        var count = 0;
        foreach (var item in cloudItems)
        {
            ct.ThrowIfCancellationRequested();
            if (await existsAsync(item)) continue;

            await addAsync(remapToLocal(item));
            count++;
        }
        return count;
    }

    /// <summary>
    /// Per-record upsert merge strategy: for each cloud item, look up the local
    /// record; if found, update it, otherwise create and add a new one.
    /// Returns the number of items processed (updated + added).
    /// </summary>
    protected static async Task<int> UpsertEachAsync(
        IReadOnlyList<TModel> cloudItems,
        Func<TModel, Task<TModel?>> findLocalAsync,
        Func<TModel, TModel, Task> updateLocalAsync,
        Func<TModel, Task> addLocalAsync,
        CancellationToken ct)
    {
        var count = 0;
        foreach (var cloudItem in cloudItems)
        {
            ct.ThrowIfCancellationRequested();
            var local = await findLocalAsync(cloudItem);
            if (local is not null)
                await updateLocalAsync(local, cloudItem);
            else
                await addLocalAsync(cloudItem);
            count++;
        }
        return count;
    }
}
