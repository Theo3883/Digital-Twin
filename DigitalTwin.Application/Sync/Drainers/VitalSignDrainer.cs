using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync.Drainers;

/// <summary>
/// Bidirectional sync for <c>VitalSign</c> rows.
///
/// PUSH: dirty local vitals → cloud (batch insert, maps local PatientId → cloud PatientId).
/// PULL: for each local patient, fetch recent cloud vitals (last 7 days) and add
///       any that are missing locally — scoped strictly to this device's patient.
/// Must run after <see cref="PatientDrainer"/>.
/// </summary>
public sealed class VitalSignDrainer(
    IVitalSignRepository local,
    IVitalSignRepository? cloud,
    IPatientRepository patient,
    IPatientRepository? cloudPatient,
    ILogger<VitalSignDrainer> logger)
    : ITableDrainer
{
    private const int ChunkSize = 100;
    private static readonly TimeSpan PurgeOlderThan = TimeSpan.FromDays(7);
    private static readonly TimeSpan PullWindow = TimeSpan.FromDays(7);

    public int Order => 3;
    public string TableName => "VitalSigns";

    public async Task<int> DrainAsync(CancellationToken ct = default)
    {
        if (cloud is null || cloudPatient is null)
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("[{Table}] Cloud repository not configured — skipping.", TableName);
            return 0;
        }

        var pushed = await PushAsync(ct);
        var pulled = await PullAsync(ct);
        return pushed + pulled;
    }

    // ── PUSH ─────────────────────────────────────────────────────────────────

    private async Task<int> PushAsync(CancellationToken ct)
    {
        var dirty = (await local.GetDirtyAsync()).ToList();
        if (dirty.Count == 0) return 0;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("[{Table}] Pushing {Count} dirty rows to cloud.", TableName, dirty.Count);

        var mapped = await MapToCloudPatientIdsAsync(dirty, ct);
        if (mapped.Count == 0)
        {
            logger.LogWarning("[{Table}] No vitals could be mapped to cloud Patient — skipping (will retry).", TableName);
            return 0;
        }

        foreach (var chunk in mapped.Chunk(ChunkSize))
        {
            ct.ThrowIfCancellationRequested();
            await cloud!.AddRangeAsync(chunk);
        }

        foreach (var group in dirty.GroupBy(v => v.PatientId))
            await local.MarkSyncedAsync(group.Key, group.Max(v => v.Timestamp));

        await local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);
        return dirty.Count;
    }

    private async Task<List<VitalSign>> MapToCloudPatientIdsAsync(List<VitalSign> vitals, CancellationToken ct)
    {
        var result = new List<VitalSign>();
        var localToCloud = new Dictionary<Guid, Guid>();

        foreach (var v in vitals)
        {
            ct.ThrowIfCancellationRequested();

            if (!localToCloud.TryGetValue(v.PatientId, out var cloudPatientId))
            {
                var localPatient = await patient.GetByIdAsync(v.PatientId);
                if (localPatient is null)
                {
                    logger.LogWarning("[{Table}] Local Patient {Id} not found — vital skipped.", TableName, v.PatientId);
                    continue;
                }

                var cloudPatient1 = await cloudPatient!.GetByUserIdAsync(localPatient.UserId);
                if (cloudPatient1 is null)
                {
                    logger.LogWarning("[{Table}] Cloud Patient for UserId {UserId} not found — ensure PatientDrainer runs first.", TableName, localPatient.UserId);
                    continue;
                }

                cloudPatientId = cloudPatient1.Id;
                localToCloud[v.PatientId] = cloudPatientId;
            }

            result.Add(new VitalSign
            {
                PatientId = cloudPatientId,
                Type = v.Type,
                Value = v.Value,
                Unit = v.Unit,
                Source = v.Source,
                Timestamp = v.Timestamp
            });
        }

        return result;
    }

    // ── PULL ──────────────────────────────────────────────────────────────────
    // Scoped to local patients only — this device only caches its own patient's vitals.

    private async Task<int> PullAsync(CancellationToken ct)
    {
        var localPatients = (await patient.GetAllAsync()).ToList();
        var since = DateTime.UtcNow - PullWindow;
        int count = 0;

        foreach (var localPatient in localPatients)
        {
            ct.ThrowIfCancellationRequested();

            var cloudPatient1 = await cloudPatient!.GetByUserIdAsync(localPatient.UserId);
            if (cloudPatient1 is null) continue;

            var cloudVitals = (await cloud!.GetByPatientAsync(cloudPatient1.Id, from: since)).ToList();

            foreach (var v in cloudVitals)
            {
                ct.ThrowIfCancellationRequested();
                if (await local.ExistsAsync(localPatient.Id, v.Type, v.Timestamp)) continue;

                await local.AddAsync(new VitalSign
                {
                    PatientId = localPatient.Id,
                    Type      = v.Type,
                    Value     = v.Value,
                    Unit      = v.Unit,
                    Source    = v.Source,
                    Timestamp = v.Timestamp
                });
                count++;
            }
        }

        if (count > 0 && logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("[{Table}] Pulled {Count} new vitals from cloud.", TableName, count);

        return count;
    }
}
