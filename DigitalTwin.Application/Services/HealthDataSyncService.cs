using System.Collections.Concurrent;
using System.Reactive.Disposables;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Sync.Drainers;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using System.Linq;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Singleton background service with two responsibilities:
///
/// 1. TABLE DRAIN — every 60 s (and on connectivity restore) calls every registered
///    <see cref="ITableDrainer"/> in sequence. Always runs once authenticated.
///
/// 2. VITALS COLLECTION — subscribes to live <see cref="IHealthDataProvider"/> stream,
///    buffers readings in a lock-free queue, and flushes them every 30 s (or eagerly at
///    100 items). Only starts when a Patient profile exists. Each flush tries the cloud
///    DB first; on failure it caches locally with IsDirty=true for the drain cycle.
/// </summary>
public class HealthDataSyncService : IHealthDataSyncService
{
    // ── Tuning ───────────────────────────────────────────────────────────────────
    private const int MaxBufferSize  = 500;
    private const int FlushThreshold = 100;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DrainInterval = TimeSpan.FromSeconds(10);

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
    private bool _vitalsActive;

    public HealthDataSyncService(IServiceScopeFactory scopeFactory, ILogger<HealthDataSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    // ── Public API ───────────────────────────────────────────────────────────────

    public async Task StartSyncAsync()
    {
        StopSync();

        // Always start the drain timer so User/UserOAuth/Patient records sync to cloud.
        _drainTimer = new Timer(_ => _ = Task.Run(PushToCloudAsync), null, DrainInterval, DrainInterval);
        _logger.LogInformation("[Sync] Drain timer started (every {Sec}s).", DrainInterval.TotalSeconds);

        // Try to start vitals collection if a patient profile exists.
        using var bootstrap = _scopeFactory.CreateScope();
        var patientId = await bootstrap.ServiceProvider
            .GetRequiredService<IAuthApplicationService>()
            .GetCurrentPatientIdAsync();

        if (patientId is not null)
        {
            StartVitalsInternal(patientId.Value);
        }
        else
        {
            _logger.LogInformation("[Sync] No patient profile yet — vitals collection deferred.");
        }
    }

    public async Task StartVitalsCollectionAsync()
    {
        if (_vitalsActive)
        {
            _logger.LogDebug("[Sync] Vitals collection already active — nothing to do.");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var patientId = await scope.ServiceProvider
            .GetRequiredService<IAuthApplicationService>()
            .GetCurrentPatientIdAsync();

        if (patientId is null)
        {
            _logger.LogWarning("[Sync] StartVitalsCollectionAsync called but no patient profile exists.");
            return;
        }

        StartVitalsInternal(patientId.Value);
    }

    public void StopSync()
    {
        _flushTimer?.Dispose(); _flushTimer = null;
        _drainTimer?.Dispose(); _drainTimer = null;
        _session?.Dispose();    _session    = null;
        _vitalsActive = false;
        _logger.LogInformation("[Sync] Stopped.");
    }

    public async Task SyncBatchAsync(IEnumerable<VitalSign> vitals)
    {
        if (_patientId == 0)
        {
            _logger.LogWarning("[Sync] SyncBatchAsync skipped — no patient profile.");
            return;
        }
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

    // ── Vitals: start / receive → queue → flush ─────────────────────────────────

    private void StartVitalsInternal(long patientId)
    {
        _patientId = patientId;

        var session       = new CompositeDisposable();
        var providerScope = _scopeFactory.CreateScope();
        session.Add(providerScope);

        var sub = providerScope.ServiceProvider
            .GetRequiredService<IHealthDataProvider>()
            .GetLiveVitals()
            .Subscribe(OnVitalReceived, ex => _logger.LogError(ex, "[Sync] Health provider stream faulted."));

        session.Add(sub);
        _session = session;

        _flushTimer = new Timer(_ => _ = Task.Run(FlushAsync), null, FlushInterval, FlushInterval);
        _vitalsActive = true;
        _logger.LogInformation("[Sync] Vitals collection started for PatientId={PatientId}.", _patientId);
    }

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
    /// Maps local PatientId → cloud PatientId via UserId (local and cloud use different ID spaces).
    /// </summary>
    private async Task PersistAsync(List<VitalSign> batch)
    {
        if (_patientId == 0)
        {
            _logger.LogWarning("[Sync] PersistAsync skipped — no patient profile.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var cloud = scope.ServiceProvider.GetKeyedService<IVitalSignRepository>("Cloud");
            var localPatient = scope.ServiceProvider.GetRequiredService<IPatientRepository>();
            var cloudPatient = scope.ServiceProvider.GetKeyedService<IPatientRepository>("Cloud");

            if (cloud is not null && cloudPatient is not null)
            {
                var patient = await localPatient.GetByIdAsync(_patientId);
                if (patient is not null)
                {
                    var cloudP = await cloudPatient.GetByUserIdAsync(patient.UserId);
                    if (cloudP is not null)
                    {
                        var cloudBatch = batch.Select(v => new VitalSign
                        {
                            PatientId = cloudP.Id,
                            Type = v.Type,
                            Value = v.Value,
                            Unit = v.Unit,
                            Source = v.Source,
                            Timestamp = v.Timestamp
                        }).ToList();
                        await cloud.AddRangeAsync(cloudBatch);
                        _logger.LogInformation("[Sync] {Count} vitals written directly to cloud.", batch.Count);
                        return;
                    }
                }
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
        var drainers = scope.ServiceProvider.GetServices<ITableDrainer>().OrderBy(d => d.Order);

        foreach (var drainer in drainers)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var count = await drainer.DrainAsync(ct);
                if (count > 0)
                    _logger.LogInformation("[Sync] {Table}: {Count} records drained.", drainer.TableName, count);
            }
            catch (Exception ex) when (IsTransientCloudFailure(ex))
            {
                _logger.LogWarning("[Sync] {Table}: cloud unavailable ({Message}) — skipping, will retry next cycle.", drainer.TableName, ex.InnerException?.Message ?? ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Sync] {Table}: drain failed — will retry next cycle.", drainer.TableName);
            }
        }
    }

    private static bool IsTransientCloudFailure(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            var name = e.GetType().FullName ?? "";
            if (name.Contains("NpgsqlException") || e is System.Net.Sockets.SocketException)
                return true;
        }
        return false;
    }
}
