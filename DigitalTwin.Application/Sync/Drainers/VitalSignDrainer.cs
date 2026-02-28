using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync.Drainers;

/// <summary>
/// Drains dirty <c>VitalSign</c> rows from local SQLite to the cloud database.
/// Maps local PatientId → cloud PatientId via UserId (local and cloud use different ID spaces).
/// Must run after <see cref="PatientDrainer"/> so the cloud Patient exists.
/// </summary>
public sealed class VitalSignDrainer : ITableDrainer
{
    private const int ChunkSize = 100;
    private static readonly TimeSpan PurgeOlderThan = TimeSpan.FromDays(7);

    private readonly IVitalSignRepository _local;
    private readonly IVitalSignRepository? _cloud;
    private readonly IPatientRepository _localPatient;
    private readonly IPatientRepository? _cloudPatient;
    private readonly ILogger<VitalSignDrainer> _logger;

    public int Order => 3;
    public string TableName => "VitalSigns";

    public VitalSignDrainer(
        IVitalSignRepository local,
        IVitalSignRepository? cloud,
        IPatientRepository localPatient,
        IPatientRepository? cloudPatient,
        ILogger<VitalSignDrainer> logger)
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
            _logger.LogDebug("[{Table}] Cloud repository not configured — skipping.", TableName);
            return 0;
        }

        var dirty = (await _local.GetDirtyAsync()).ToList();
        if (dirty.Count == 0) return 0;

        _logger.LogInformation("[{Table}] Draining {Count} dirty rows to cloud.", TableName, dirty.Count);

        // Map local PatientId → cloud PatientId. Local and cloud use different ID spaces.
        var mapped = await MapToCloudPatientIdsAsync(dirty, ct);
        if (mapped.Count == 0)
        {
            _logger.LogWarning("[{Table}] No vitals could be mapped to cloud Patient — skipping (will retry).", TableName);
            return 0;
        }

        foreach (var chunk in mapped.Chunk(ChunkSize))
        {
            ct.ThrowIfCancellationRequested();
            await _cloud.AddRangeAsync(chunk);
        }

        foreach (var group in dirty.GroupBy(v => v.PatientId))
            await _local.MarkSyncedAsync(group.Key, group.Max(v => v.Timestamp));

        await _local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);

        return dirty.Count;
    }

    private async Task<List<VitalSign>> MapToCloudPatientIdsAsync(List<VitalSign> vitals, CancellationToken ct)
    {
        var result = new List<VitalSign>();
        var localToCloud = new Dictionary<long, long>();

        foreach (var v in vitals)
        {
            ct.ThrowIfCancellationRequested();

            if (!localToCloud.TryGetValue(v.PatientId, out var cloudPatientId))
            {
                var localPatient = await _localPatient.GetByIdAsync(v.PatientId);
                if (localPatient is null)
                {
                    _logger.LogWarning("[{Table}] Local Patient {Id} not found — vital skipped.", TableName, v.PatientId);
                    continue;
                }

                var cloudPatient = await _cloudPatient!.GetByUserIdAsync(localPatient.UserId);
                if (cloudPatient is null)
                {
                    _logger.LogWarning("[{Table}] Cloud Patient for UserId {UserId} not found — ensure PatientDrainer runs first.", TableName, localPatient.UserId);
                    continue;
                }

                cloudPatientId = cloudPatient.Id;
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
}
