using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync.Drainers;

/// <summary>
/// Bidirectional sync for <c>SleepSession</c> rows.
///
/// PUSH: dirty local sessions → cloud (batch insert, maps local PatientId → cloud PatientId).
/// PULL: for each local patient, fetch cloud sleep sessions (last 30 days) and add
///       any missing to local cache — scoped strictly to this device's patient.
/// Must run after <see cref="PatientDrainer"/>.
/// </summary>
public sealed class SleepSessionDrainer : ITableDrainer
{
    private const int ChunkSize = 50;
    private static readonly TimeSpan PurgeOlderThan = TimeSpan.FromDays(30);
    private static readonly TimeSpan PullWindow = TimeSpan.FromDays(30);

    private readonly ISleepSessionRepository _local;
    private readonly ISleepSessionRepository? _cloud;
    private readonly IPatientRepository _localPatient;
    private readonly IPatientRepository? _cloudPatient;
    private readonly ILogger<SleepSessionDrainer> _logger;

    public int Order => 5;
    public string TableName => "SleepSessions";

    public SleepSessionDrainer(
        ISleepSessionRepository local,
        ISleepSessionRepository? cloud,
        IPatientRepository localPatient,
        IPatientRepository? cloudPatient,
        ILogger<SleepSessionDrainer> logger)
    {
        _local = local;
        _cloud = cloud;
        _localPatient = localPatient;
        _cloudPatient = cloudPatient;
        _logger = logger;
    }

    public async Task<int> DrainAsync(CancellationToken ct = default)
    {
        if (_cloud is null || _cloudPatient is null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("[{Table}] Cloud repository not configured — skipping.", TableName);
            return 0;
        }

        var pushed = await PushAsync(ct);
        var pulled = await PullAsync(ct);
        return pushed + pulled;
    }

    // ── PUSH ─────────────────────────────────────────────────────────────────

    private async Task<int> PushAsync(CancellationToken ct)
    {
        var dirty = (await _local.GetDirtyAsync()).ToList();
        if (dirty.Count == 0) return 0;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[{Table}] Pushing {Count} dirty rows to cloud.", TableName, dirty.Count);

        var mapped = await MapToCloudPatientIdsAsync(dirty, ct);
        if (mapped.Count == 0)
        {
            _logger.LogWarning("[{Table}] No sleep sessions could be mapped to cloud Patient — skipping (will retry).", TableName);
            return 0;
        }

        foreach (var chunk in mapped.Chunk(ChunkSize))
        {
            ct.ThrowIfCancellationRequested();
            await _cloud!.AddRangeAsync(chunk);
        }

        foreach (var group in dirty.GroupBy(s => s.PatientId))
            await _local.MarkSyncedAsync(group.Key, group.Max(s => s.StartTime));

        await _local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);
        return dirty.Count;
    }

    private async Task<List<SleepSession>> MapToCloudPatientIdsAsync(
        List<SleepSession> sessions, CancellationToken ct)
    {
        var result = new List<SleepSession>();
        var localToCloud = new Dictionary<Guid, Guid>();

        foreach (var s in sessions)
        {
            ct.ThrowIfCancellationRequested();

            if (!localToCloud.TryGetValue(s.PatientId, out var cloudPatientId))
            {
                var localPatient = await _localPatient.GetByIdAsync(s.PatientId);
                if (localPatient is null)
                {
                    _logger.LogWarning("[{Table}] Local Patient {Id} not found — session skipped.", TableName, s.PatientId);
                    continue;
                }

                var cloudPatient = await _cloudPatient!.GetByUserIdAsync(localPatient.UserId);
                if (cloudPatient is null)
                {
                    _logger.LogWarning("[{Table}] Cloud Patient for UserId {UserId} not found — ensure PatientDrainer runs first.", TableName, localPatient.UserId);
                    continue;
                }

                cloudPatientId = cloudPatient.Id;
                localToCloud[s.PatientId] = cloudPatientId;
            }

            result.Add(new SleepSession
            {
                PatientId        = cloudPatientId,
                StartTime        = s.StartTime,
                EndTime          = s.EndTime,
                DurationMinutes  = s.DurationMinutes,
                QualityScore     = s.QualityScore
            });
        }

        return result;
    }

    // ── PULL ──────────────────────────────────────────────────────────────────
    // Scoped to local patients only — this device only caches its own patient's sleep data.

    private async Task<int> PullAsync(CancellationToken ct)
    {
        var localPatients = (await _localPatient.GetAllAsync()).ToList();
        var since = DateTime.UtcNow - PullWindow;
        int count = 0;

        foreach (var localPatient in localPatients)
        {
            ct.ThrowIfCancellationRequested();

            var cloudPatient = await _cloudPatient!.GetByUserIdAsync(localPatient.UserId);
            if (cloudPatient is null) continue;

            var cloudSessions = (await _cloud!.GetByPatientAsync(cloudPatient.Id, from: since)).ToList();

            foreach (var s in cloudSessions)
            {
                ct.ThrowIfCancellationRequested();
                if (await _local.ExistsAsync(localPatient.Id, s.StartTime)) continue;

                await _local.AddAsync(new SleepSession
                {
                    PatientId       = localPatient.Id,
                    StartTime       = s.StartTime,
                    EndTime         = s.EndTime,
                    DurationMinutes = s.DurationMinutes,
                    QualityScore    = s.QualityScore
                });
                count++;
            }
        }

        if (count > 0 && _logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[{Table}] Pulled {Count} new sleep sessions from cloud.", TableName, count);

        return count;
    }
}
