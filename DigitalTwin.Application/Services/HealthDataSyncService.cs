using System.Collections.Concurrent;
using System.Reactive.Disposables;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Interfaces.Sync;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using System.Linq;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Synchronizes live and cached health data between local storage and cloud persistence.
/// </summary>
public class HealthDataSyncService : IHealthDataSyncService
{
    // ── Tuning ───────────────────────────────────────────────────────────────────
    private const int MaxBufferSize  = 5000; 
    private const int FlushThreshold = 100;    // flush eagerly once 100 readings queue up
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(600);  // flush timer every 60 s
    private static readonly TimeSpan DrainInterval = TimeSpan.FromSeconds(300);
    private static readonly TimeSpan SleepCollectInterval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HealthDataSyncService> _logger;
    private readonly ITransientFailurePolicy _transientPolicy;

    // ── State ────────────────────────────────────────────────────────────────────
    private readonly ConcurrentQueue<VitalSign> _queue  = new();
    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private readonly SemaphoreSlim _drainGate = new(1, 1);

    private CompositeDisposable? _session;
    private Timer? _flushTimer;
    private Timer? _drainTimer;
    private Timer? _sleepTimer;
    private Guid _patientId;
    private bool _vitalsActive;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthDataSyncService"/> class.
    /// </summary>
    public HealthDataSyncService(
        IServiceScopeFactory scopeFactory,
        ILogger<HealthDataSyncService> logger,
        ITransientFailurePolicy transientPolicy)
    {
        _scopeFactory    = scopeFactory;
        _logger          = logger;
        _transientPolicy = transientPolicy;
    }

    /// <summary>
    /// Starts background synchronization and begins vitals collection when a patient profile exists.
    /// </summary>
    public async Task StartSyncAsync()
    {
        StopSync();

        // Always start the drain timer so User/UserOAuth/Patient records sync to cloud.
        _drainTimer = new Timer(_ => Task.Run(PushToCloudAsync), null, DrainInterval, DrainInterval);
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[Sync] Drain timer started (every {Sec}s).", DrainInterval.TotalSeconds);

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

    /// <summary>
    /// Starts live vitals collection for the current patient profile.
    /// </summary>
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

    /// <summary>
    /// Stops active timers, subscriptions, and collection state.
    /// </summary>
    public void StopSync()
    {
        _flushTimer?.Dispose(); _flushTimer = null;
        _drainTimer?.Dispose(); _drainTimer = null;
        _sleepTimer?.Dispose(); _sleepTimer = null;
        _session?.Dispose();    _session    = null;
        _vitalsActive = false;
        _logger.LogInformation("[Sync] Stopped.");
    }

    /// <summary>
    /// Persists an externally supplied batch of vital signs for the current patient.
    /// </summary>
    public async Task SyncBatchAsync(IEnumerable<VitalSign> vitals)
    {
        if (_patientId == Guid.Empty)
        {
            _logger.LogWarning("[Sync] SyncBatchAsync skipped — no patient profile.");
            return;
        }
        var batch = vitals.ToList();
        if (batch.Count > 0) await PersistAsync(batch);
    }

    /// <summary>
    /// Pushes all locally dirty data tables to cloud storage.
    /// </summary>
    public async Task PushToCloudAsync()
    {
        if (!await _drainGate.WaitAsync(0)) return;
        try   { await DrainAllTablesAsync(); }
        finally { _drainGate.Release(); }
    }

    private void StartVitalsInternal(Guid patientId)
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

        _flushTimer = new Timer(_ => Task.Run(FlushAsync), null, FlushInterval, FlushInterval);
        _sleepTimer = new Timer(_ => Task.Run(CollectSleepAsync), null, TimeSpan.FromSeconds(5), SleepCollectInterval);
        _vitalsActive = true;
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[Sync] Vitals collection started for PatientId={PatientId}.", _patientId);
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
        if (_queue.Count >= FlushThreshold) Task.Run(FlushAsync);
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
    /// Writes <paramref name="batch"/> to local SQLite always, and to the cloud when reachable.
    ///
    /// Strategy:
    ///   1. Try cloud write — if it succeeds, mark the local copy as synced (IsDirty=false)
    ///      so the drain timer does not push the same records again.
    ///   2. If cloud is unreachable/fails, write locally with IsDirty=true so the drain
    ///      timer picks them up on the next cycle.
    ///
    /// This guarantees the local DB always holds the complete vital-sign history,
    /// which is required for offline access and for the Home page history charts.
    /// </summary>
    private async Task PersistAsync(List<VitalSign> batch)
    {
        if (_patientId == Guid.Empty)
        {
            _logger.LogWarning("[Sync] PersistAsync skipped — no patient profile.");
            return;
        }

        var cloudSucceeded = await TryWriteToCloudAsync(batch);

        // Always persist locally. If cloud already stored them, mark as synced
        // (IsDirty=false) so the drain timer won't push duplicates.
        await WriteLocallyAsync(batch, markDirty: !cloudSucceeded);
    }

    private async Task<bool> TryWriteToCloudAsync(List<VitalSign> batch)
    {
        try
        {
            using var scope       = _scopeFactory.CreateScope();
            var cloud             = scope.ServiceProvider.GetKeyedService<IVitalSignRepository>("Cloud");
            var localPatientRepo  = scope.ServiceProvider.GetRequiredService<IPatientRepository>();
            var cloudPatientRepo  = scope.ServiceProvider.GetKeyedService<IPatientRepository>("Cloud");

            if (cloud is null || cloudPatientRepo is null)
                return false;

            var patient = await localPatientRepo.GetByIdAsync(_patientId);
            if (patient is null)
                return false;

            var resolver    = scope.ServiceProvider.GetRequiredService<ICloudIdentityResolver>();
            var cloudUserId = await resolver.ResolveCloudUserIdAsync(patient.UserId);
            var cloudP      = cloudUserId is not null
                ? await cloudPatientRepo.GetByUserIdAsync(cloudUserId.Value)
                : null;

            if (cloudP is null)
                return false;

            var cloudBatch = batch.Select(v => new VitalSign
            {
                PatientId = cloudP.Id,
                Type      = v.Type,
                Value     = v.Value,
                Unit      = v.Unit,
                Source    = v.Source,
                Timestamp = v.Timestamp
            }).ToList();

            await cloud.AddRangeAsync(cloudBatch);
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("[Sync] {Count} vitals written directly to cloud.", batch.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Sync] Cloud write failed for {Count} vitals; caching locally.", batch.Count);
            return false;
        }
    }

    private async Task WriteLocallyAsync(List<VitalSign> batch, bool markDirty = true)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var local = scope.ServiceProvider.GetRequiredService<IVitalSignRepository>();
            await local.AddRangeAsync(batch, markDirty);
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("[Sync] {Count} vitals saved locally (dirty={Dirty}).", batch.Count, markDirty);
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

    // ── Sleep collection ────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches recent sleep sessions from <see cref="IHealthDataProvider"/> and persists
    /// them locally. Deduplication is done via <c>ExistsAsync</c>.
    /// </summary>
    private async Task CollectSleepAsync()
    {
        if (_patientId == Guid.Empty) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<IHealthDataProvider>();
            var repo = scope.ServiceProvider.GetRequiredService<ISleepSessionRepository>();
            var cloudRepo = scope.ServiceProvider.GetKeyedService<ISleepSessionRepository>("Cloud");

            var to = DateTime.UtcNow;
            var from = to.AddDays(-3); // look back 3 days to catch late-arriving data

            var sessions = (await provider.GetSleepSessionsAsync(from, to)).ToList();
            if (sessions.Count == 0) return;

            var inserted = 0;
            foreach (var s in sessions)
            {
                s.PatientId = _patientId;
                if (await repo.ExistsAsync(s.PatientId, s.StartTime))
                    continue;

                var cloudSucceeded = false;
                if (cloudRepo != null)
                {
                    try
                    {
                        await cloudRepo.AddAsync(s, markDirty: false);
                        cloudSucceeded = true;
                    }
                    catch { /* will sync via drainer */ }
                }

                await repo.AddAsync(s, markDirty: !cloudSucceeded);
                inserted++;
            }

            if (inserted > 0 && _logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("[Sync] {Count} sleep sessions collected and persisted locally.", inserted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Sync] Sleep session collection failed.");
        }
    }

    // ── Table drain ──────────────────────────────────────────────────────────────

    private async Task DrainAllTablesAsync(CancellationToken ct = default)
    {
        using var scope   = _scopeFactory.CreateScope();
        var drainers = scope.ServiceProvider.GetServices<ISyncDrainer>().OrderBy(d => d.Order);

        foreach (var drainer in drainers)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var count = await drainer.DrainAsync(ct);
                if (count > 0 && _logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("[Sync] {Table}: {Count} records drained.", drainer.TableName, count);
            }
            catch (Exception ex) when (_transientPolicy.IsTransient(ex))
            {
                _logger.LogWarning(ex, "[Sync] {Table}: cloud unavailable — skipping, will retry next cycle.", drainer.TableName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Sync] {Table}: drain failed — will retry next cycle.", drainer.TableName);
            }
        }
    }

}
