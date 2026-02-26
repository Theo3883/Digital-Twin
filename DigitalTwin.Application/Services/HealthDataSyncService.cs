using System.Collections.Concurrent;
using System.Reactive.Disposables;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Sync.Drainers;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Singleton background service with two responsibilities:
///
/// 1. VITALS COLLECTION — subscribes to live <see cref="IHealthDataProvider"/> stream,
///    buffers readings in a lock-free queue, and flushes them every 30 s (or eagerly at
///    100 items). Each flush tries the cloud DB first; on failure it caches locally
///    with IsDirty=true for the drain cycle to pick up.
///
/// 2. TABLE DRAIN — every 60 s (and on connectivity restore) calls every registered
///    <see cref="ITableDrainer"/> in sequence. FAIL-FAST: the first drainer that throws
///    stops the whole cycle; all tables retry on the next tick.
/// </summary>
public class HealthDataSyncService : IHealthDataSyncService
{
    // ── Tuning ───────────────────────────────────────────────────────────────────
    private const int MaxBufferSize  = 500;
    private const int FlushThreshold = 100;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DrainInterval = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HealthDataSyncService> _logger;

    // ── State ────────────────────────────────────────────────────────────────────
    private readonly ConcurrentQueue<VitalSign> _queue  = new();
    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private readonly SemaphoreSlim _drainGate = new(1, 1);

    private CompositeDisposable? _session;
    private Timer? _flushTimer;
    private Timer? _drainTimer;
    private long _patientId;

    public HealthDataSyncService(IServiceScopeFactory scopeFactory, ILogger<HealthDataSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    // ── Public API ───────────────────────────────────────────────────────────────

    public async Task StartSyncAsync()
    {
        StopSync();

        using var bootstrap = _scopeFactory.CreateScope();
        var patientId = await bootstrap.ServiceProvider
            .GetRequiredService<IAuthApplicationService>()
            .GetCurrentPatientIdAsync();

        if (patientId is null)
        {
            _logger.LogWarning("[Sync] No authenticated patient — sync not started.");
            return;
        }
        _patientId = patientId.Value;

        var session       = new CompositeDisposable();
        var providerScope = _scopeFactory.CreateScope();
        session.Add(providerScope);

        var sub = providerScope.ServiceProvider
            .GetRequiredService<IHealthDataProvider>()
            .GetLiveVitals()
            .Subscribe(OnVitalReceived, ex => _logger.LogError(ex, "[Sync] Health provider stream faulted."));

        session.Add(sub);
        _session = session;

        _flushTimer = new Timer(_ => _ = Task.Run(FlushAsync),       null, FlushInterval, FlushInterval);
        _drainTimer = new Timer(_ => _ = Task.Run(PushToCloudAsync), null, DrainInterval, DrainInterval);

        _logger.LogInformation("[Sync] Started for PatientId={PatientId}.", _patientId);
    }

    public void StopSync()
    {
        _flushTimer?.Dispose(); _flushTimer = null;
        _drainTimer?.Dispose(); _drainTimer = null;
        _session?.Dispose();    _session    = null;
        _logger.LogInformation("[Sync] Stopped.");
    }

    public async Task SyncBatchAsync(IEnumerable<VitalSign> vitals)
    {
        var batch = vitals.ToList();
        if (batch.Count > 0) await PersistAsync(batch);
    }

    /// <summary>
    /// Drains ALL dirty tables to the cloud in registration order.
    /// Called by the drain timer and by <see cref="ConnectivityMonitor"/> on reconnect.
    /// </summary>
    public async Task PushToCloudAsync()
    {
        if (!await _drainGate.WaitAsync(0)) return;
        try   { await DrainAllTablesAsync(); }
        finally { _drainGate.Release(); }
    }

    // ── Vitals: receive → queue → flush ─────────────────────────────────────────

    private void OnVitalReceived(VitalSign vital)
    {
        vital.PatientId = _patientId;
        if (string.IsNullOrEmpty(vital.Source)) vital.Source = "HealthKit";

        if (_queue.Count >= MaxBufferSize)
        {
            _queue.TryDequeue(out _);
            _logger.LogWarning("[Sync] Buffer full ({Max}); oldest vital dropped.", MaxBufferSize);
        }

        _queue.Enqueue(vital);
        if (_queue.Count >= FlushThreshold) _ = Task.Run(FlushAsync);
    }

    private async Task FlushAsync()
    {
        if (!await _flushGate.WaitAsync(TimeSpan.Zero)) return;
        try
        {
            var batch = DrainQueue();
            if (batch.Count > 0) await PersistAsync(batch);
        }
        catch (Exception ex) { _logger.LogError(ex, "[Sync] FlushAsync failed."); }
        finally { _flushGate.Release(); }
    }

    /// <summary>
    /// Cloud-first: write directly to the cloud DB; on any failure cache locally
    /// with IsDirty=true so the drain timer picks it up on the next cycle.
    /// </summary>
    private async Task PersistAsync(List<VitalSign> batch)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var cloud = scope.ServiceProvider.GetKeyedService<IVitalSignRepository>("Cloud");
            if (cloud is not null)
            {
                await cloud.AddRangeAsync(batch);
                _logger.LogInformation("[Sync] {Count} vitals written directly to cloud.", batch.Count);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Sync] Cloud write failed for {Count} vitals; caching locally.", batch.Count);
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var local = scope.ServiceProvider.GetRequiredService<IVitalSignRepository>();
            await local.AddRangeAsync(batch);
            _logger.LogInformation("[Sync] {Count} vitals cached locally (dirty=true).", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Sync] Local cache failed for {Count} vitals; batch discarded.", batch.Count);
        }
    }

    private List<VitalSign> DrainQueue()
    {
        var batch = new List<VitalSign>();
        while (_queue.TryDequeue(out var item)) batch.Add(item);
        return batch;
    }

    // ── Table drain ──────────────────────────────────────────────────────────────

    private async Task DrainAllTablesAsync(CancellationToken ct = default)
    {
        using var scope   = _scopeFactory.CreateScope();
        var drainers = scope.ServiceProvider.GetServices<ITableDrainer>();

        foreach (var drainer in drainers)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var count = await drainer.DrainAsync(ct);
                if (count > 0)
                    _logger.LogInformation("[Sync] {Table}: {Count} records drained.", drainer.TableName, count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Sync] {Table}: drain failed — all tables retry next cycle.", drainer.TableName);
                throw;
            }
        }
    }
}
