using System.Collections.Concurrent;
using System.Reactive.Disposables;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Sync;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Singleton background service that collects live vitals and persists them.
///
/// Strategy:
///   1. Buffer vitals in a lock-free ConcurrentQueue (never blocks the Rx thread).
///   2. Every 30 s (or when the buffer hits FlushThreshold), drain the queue.
///   3. CLOUD-FIRST: try to write the batch directly to the cloud PostgreSQL DB.
///      - Success → done. Local SQLite is never touched for this batch.
///      - Failure (cloud offline, not configured, any exception) → fall back silently.
///   4. LOCAL FALLBACK: write the batch to local SQLite with IsDirty=true.
///   5. A 60-second drain timer retries dirty local records → cloud → marks synced → purges old.
///   6. Every exception at every level is caught and logged; the app NEVER crashes from sync.
///
/// Registered as Singleton so the queue, timers, and subscription survive across page
/// navigations. IServiceScopeFactory is used to create a fresh DI scope (and therefore
/// a fresh DbContext) for every DB operation — eliminating all concurrent-context issues.
/// </summary>
public class HealthDataSyncService : IHealthDataSyncService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HealthDataSyncService> _logger;

    // ── Tuning constants ────────────────────────────────────────────────────────
    private const int MaxBufferSize  = 500;   // drop-oldest safety valve
    private const int FlushThreshold = 100;   // eager flush when queue reaches this
    private static readonly TimeSpan FlushInterval  = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DrainInterval  = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PurgeOlderThan = TimeSpan.FromDays(7);

    // ── State ───────────────────────────────────────────────────────────────────
    private readonly ConcurrentQueue<VitalSign> _queue = new();
    private readonly SemaphoreSlim _flushGate = new(1, 1); // 1 flush at a time
    private readonly SemaphoreSlim _drainGate  = new(1, 1); // 1 drain at a time

    // Holds the provider subscription AND the long-lived provider scope.
    // Disposed atomically in StopSync().
    private CompositeDisposable? _session;
    private Timer? _flushTimer;
    private Timer? _drainTimer;

    private long _patientId;

    // ── Constructor ─────────────────────────────────────────────────────────────
    public HealthDataSyncService(
        IServiceScopeFactory scopeFactory,
        ILogger<HealthDataSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ── Public API ──────────────────────────────────────────────────────────────

    public async Task StartSyncAsync()
    {
        // Idempotent: stop any previous session before starting a new one.
        StopSync();

        // ── Resolve patient identity ──────────────────────────────────────────
        // Use a temporary scope; we only need the auth service to read the id.
        long? patientId;
        using (var bootstrap = _scopeFactory.CreateScope())
        {
            var auth = bootstrap.ServiceProvider.GetRequiredService<IAuthApplicationService>();
            patientId = await auth.GetCurrentPatientIdAsync();
        }

        if (patientId is null)
        {
            _logger.LogWarning("[Sync] No authenticated patient \u2014 sync not started.");
            return;
        }
        _patientId = patientId.Value;

        // ── Build the session ─────────────────────────────────────────────────
        // The provider scope lives for the entire sync session (until StopSync).
        // Adding it to the CompositeDisposable ensures it is disposed with the session.
        var session = new CompositeDisposable();
        var providerScope = _scopeFactory.CreateScope();
        session.Add(providerScope);

        var healthProvider = providerScope.ServiceProvider.GetRequiredService<IHealthDataProvider>();

        var sub = healthProvider.GetLiveVitals()
            .Subscribe(vital =>
            {
                vital.PatientId = _patientId;
                if (string.IsNullOrEmpty(vital.Source))
                    vital.Source = "HealthKit";

                // Safety valve: drop the oldest entry so we never run out of memory.
                if (_queue.Count >= MaxBufferSize)
                {
                    _queue.TryDequeue(out _);
                    _logger.LogWarning("[Sync] Buffer full ({Max}); oldest vital dropped.", MaxBufferSize);
                }

                _queue.Enqueue(vital);

                // Eagerly flush when we have enough data — no need to wait for the timer.
                if (_queue.Count >= FlushThreshold)
                    _ = Task.Run(FlushAsync);
            }, ex =>
            {
                _logger.LogError(ex, "[Sync] Health provider stream faulted.");
            });

        session.Add(sub);
        _session = session;

        // ── Start background timers ───────────────────────────────────────────
        _flushTimer = new Timer(_ => _ = Task.Run(FlushAsync),         null, FlushInterval,  FlushInterval);
        _drainTimer = new Timer(_ => _ = Task.Run(PushToCloudAsync),   null, DrainInterval,  DrainInterval);

        _logger.LogInformation("[Sync] Started for PatientId={PatientId}.", _patientId);
    }

    public void StopSync()
    {
        _flushTimer?.Dispose(); _flushTimer = null;
        _drainTimer?.Dispose(); _drainTimer = null;
        _session?.Dispose();    _session    = null;
        _logger.LogInformation("[Sync] Stopped.");
    }

    /// <summary>Manually persist a batch (e.g. from iOS BackgroundFetch).</summary>
    public async Task SyncBatchAsync(IEnumerable<VitalSign> vitals)
    {
        var batch = vitals.ToList();
        if (batch.Count == 0) return;
        await PersistBatchAsync(batch);
    }

    /// <summary>
    /// Drains dirty local records to the cloud DB.
    /// Called by the drain timer and by ConnectivityMonitor on reconnect.
    /// </summary>
    public async Task PushToCloudAsync()
    {
        // Zero-timeout: if a drain is already in progress skip this call entirely.
        if (!_drainGate.Wait(0)) return;
        try
        {
            await DrainLocalToCloudsAsync();
        }
        finally
        {
            _drainGate.Release();
        }
    }

    // ── Private implementation ───────────────────────────────────────────────────

    private async Task FlushAsync()
    {
        if (!await _flushGate.WaitAsync(TimeSpan.Zero)) return;
        try
        {
            if (_queue.IsEmpty) return;

            var batch = new List<VitalSign>();
            while (_queue.TryDequeue(out var item))
                batch.Add(item);

            if (batch.Count > 0)
                await PersistBatchAsync(batch);
        }
        catch (Exception ex)
        {
            // Safety net for any unexpected exception — never propagate out of the timer callback.
            _logger.LogError(ex, "[Sync] Unexpected error in FlushAsync.");
        }
        finally
        {
            _flushGate.Release();
        }
    }

    /// <summary>
    /// CLOUD-FIRST: try cloud write → on any failure, cache locally.
    /// Never throws.
    /// </summary>
    private async Task PersistBatchAsync(List<VitalSign> batch)
    {
        if (await TryWriteToCloudAsync(batch))
        {
            _logger.LogInformation("[Sync] {Count} vitals written directly to cloud.", batch.Count);
            return;
        }

        // Cloud unavailable or not configured — cache in local SQLite for later drain.
        await TryWriteToLocalAsync(batch);
    }

    private async Task<bool> TryWriteToCloudAsync(List<VitalSign> batch)
    {
        try
        {
            // Fresh scope → fresh CloudDbContext per batch. No shared state, no concurrency issues.
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetKeyedService<IVitalSignRepository>("Cloud");
            if (repo is null) return false; // cloud not configured

            await repo.AddRangeAsync(batch);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Sync] Cloud write failed for {Count} vitals. Falling back to local cache.", batch.Count);
            return false;
        }
    }

    private async Task TryWriteToLocalAsync(List<VitalSign> batch)
    {
        try
        {
            // Fresh scope → fresh LocalDbContext. Same factory-per-call guarantee.
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IVitalSignRepository>();
            await repo.AddRangeAsync(batch);
            _logger.LogInformation("[Sync] {Count} vitals cached in local SQLite (dirty=true).", batch.Count);
        }
        catch (Exception ex)
        {
            // Absolute last resort — log and discard. We MUST NOT crash the app.
            _logger.LogError(ex, "[Sync] Local cache write failed for {Count} vitals. Batch discarded.", batch.Count);
        }
    }

    /// <summary>
    /// Reads IsDirty records from local SQLite, uploads to cloud in chunks,
    /// marks them synced, then purges old synced records to keep storage low.
    /// </summary>
    private async Task DrainLocalToCloudsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var localRepo = scope.ServiceProvider.GetRequiredService<IVitalSignRepository>();
            var cloudRepo = scope.ServiceProvider.GetKeyedService<IVitalSignRepository>("Cloud");
            if (cloudRepo is null) return; // cloud not registered

            var dirty = (await localRepo.GetDirtyAsync()).ToList();
            if (dirty.Count == 0) return;

            _logger.LogInformation("[Sync] Drain: found {Count} dirty local vitals.", dirty.Count);

            var uploaded = new List<VitalSign>();
            foreach (var chunk in dirty.Chunk(100))
            {
                try
                {
                    await cloudRepo.AddRangeAsync(chunk);
                    uploaded.AddRange(chunk);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Sync] Drain chunk failed. Stopping this cycle; will retry next time.");
                    break; // leave remaining dirty for the next drain cycle
                }
            }

            if (uploaded.Count == 0) return;

            // Mark uploaded records synced and purge old ones to keep SQLite lean.
            foreach (var group in uploaded.GroupBy(v => v.PatientId))
            {
                var maxTs = group.Max(v => v.Timestamp);
                await localRepo.MarkSyncedAsync(group.Key, maxTs);
            }
            await localRepo.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);

            _logger.LogInformation("[Sync] Drain complete: {Count} vitals uploaded to cloud.", uploaded.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Sync] Unhandled error in DrainLocalToCloudsAsync. Will retry next cycle.");
        }
    }
}
