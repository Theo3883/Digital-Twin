using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync.Drainers;

/// <summary>
/// Drains dirty <c>Patient</c> rows from local SQLite to the cloud database.
/// Uses an upsert strategy keyed on <c>UserId</c>: existing cloud patients are
/// updated (medical notes, blood type, allergies), new patients are inserted.
/// </summary>
public sealed class PatientDrainer : ITableDrainer
{
    private static readonly TimeSpan PurgeOlderThan = TimeSpan.FromDays(7);

    private readonly IPatientRepository _local;
    private readonly IPatientRepository? _cloud;
    private readonly ILogger<PatientDrainer> _logger;

    public string TableName => "Patients";

    public PatientDrainer(
        IPatientRepository local,
        IPatientRepository? cloud,
        ILogger<PatientDrainer> logger)
    {
        _local = local;
        _cloud = cloud;
        _logger = logger;
    }

    public async Task<int> DrainAsync(CancellationToken ct = default)
    {
        if (_cloud is null)
        {
            _logger.LogDebug("[{Table}] Cloud repository not configured — skipping.", TableName);
            return 0;
        }

        var dirty = (await _local.GetDirtyAsync()).ToList();
        if (dirty.Count == 0) return 0;

        _logger.LogInformation("[{Table}] Draining {Count} dirty rows to cloud.", TableName, dirty.Count);

        foreach (var patient in dirty)
        {
            ct.ThrowIfCancellationRequested();
            await UpsertAsync(patient);
        }

        await _local.MarkSyncedAsync(dirty);
        await _local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);

        return dirty.Count;
    }

    private async Task UpsertAsync(Patient patient)
    {
        var existing = await _cloud!.GetByUserIdAsync(patient.UserId);
        if (existing is not null)
        {
            existing.BloodType = patient.BloodType;
            existing.Allergies = patient.Allergies;
            existing.MedicalHistoryNotes = patient.MedicalHistoryNotes;
            await _cloud.UpdateAsync(existing);
        }
        else
        {
            await _cloud.AddAsync(patient);
        }
    }
}
