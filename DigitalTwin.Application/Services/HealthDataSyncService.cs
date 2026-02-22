using System.Reactive.Disposables;
using System.Reactive.Linq;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Sync;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Application.Services;

public class HealthDataSyncService : IHealthDataSyncService
{
    private readonly IHealthDataProvider _healthProvider;
    private readonly IVitalSignRepository _localRepo;
    private readonly IAuthApplicationService _authService;
    private readonly ISyncFacade<VitalSign> _syncFacade;

    private readonly List<VitalSign> _buffer = [];
    private readonly object _bufferLock = new();
    private CompositeDisposable? _subscriptions;
    private Timer? _flushTimer;

    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PurgeOlderThan = TimeSpan.FromDays(7);

    public HealthDataSyncService(
        IHealthDataProvider healthProvider,
        IVitalSignRepository localRepo,
        IAuthApplicationService authService,
        ISyncFacade<VitalSign> syncFacade)
    {
        _healthProvider = healthProvider;
        _localRepo = localRepo;
        _authService = authService;
        _syncFacade = syncFacade;
    }

    public async Task StartSyncAsync()
    {
        var patientId = await _authService.GetCurrentPatientIdAsync();
        if (patientId is null) return;

        _subscriptions?.Dispose();
        _subscriptions = new CompositeDisposable();

        var sub = _healthProvider.GetLiveVitals()
            .Subscribe(vital =>
            {
                vital.PatientId = patientId.Value;
                if (string.IsNullOrEmpty(vital.Source))
                    vital.Source = "HealthKit";

                lock (_bufferLock)
                    _buffer.Add(vital);
            });

        _subscriptions.Add(sub);

        _flushTimer = new Timer(async _ => await FlushBufferAsync(), null, FlushInterval, FlushInterval);
    }

    public void StopSync()
    {
        _flushTimer?.Dispose();
        _flushTimer = null;
        _subscriptions?.Dispose();
        _subscriptions = null;
    }

    public async Task SyncBatchAsync(IEnumerable<VitalSign> vitals)
    {
        foreach (var vital in vitals)
            await _localRepo.AddAsync(vital);
    }

    public async Task PushToCloudAsync()
    {
        try
        {
            await _syncFacade.SyncAsync(PurgeOlderThan);
        }
        catch
        {
            // Cloud unreachable; will retry on next connectivity event
        }
    }

    private async Task FlushBufferAsync()
    {
        List<VitalSign> toFlush;
        lock (_bufferLock)
        {
            if (_buffer.Count == 0) return;
            toFlush = [.. _buffer];
            _buffer.Clear();
        }

        await SyncBatchAsync(toFlush);
    }
}
