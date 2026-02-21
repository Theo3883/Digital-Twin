using System.Reactive.Disposables;
using System.Reactive.Linq;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalTwin.Application.Services;

public class HealthDataSyncService : IHealthDataSyncService
{
    private readonly IHealthDataProvider _healthProvider;
    private readonly IVitalSignRepository _localRepo;
    private readonly IAuthApplicationService _authService;
    private readonly IServiceProvider _serviceProvider;

    private readonly List<VitalSign> _buffer = [];
    private readonly object _bufferLock = new();
    private CompositeDisposable? _subscriptions;
    private Timer? _flushTimer;

    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

    public HealthDataSyncService(
        IHealthDataProvider healthProvider,
        IVitalSignRepository localRepo,
        IAuthApplicationService authService,
        IServiceProvider serviceProvider)
    {
        _healthProvider = healthProvider;
        _localRepo = localRepo;
        _authService = authService;
        _serviceProvider = serviceProvider;
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
        var dirtyRecords = (await _localRepo.GetDirtyAsync()).ToList();
        if (dirtyRecords.Count == 0) return;

        try
        {
            var cloudRepo = _serviceProvider.GetKeyedService<IVitalSignRepository>("Cloud");
            if (cloudRepo is null) return;

            foreach (var record in dirtyRecords)
                await cloudRepo.AddAsync(record);

            var grouped = dirtyRecords.GroupBy(r => r.PatientId);
            foreach (var group in grouped)
            {
                var maxTimestamp = group.Max(r => r.Timestamp);
                await _localRepo.MarkSyncedAsync(group.Key, maxTimestamp);
            }
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
