using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync.Drainers;

/// <summary>
/// Drains dirty <c>Patient</c> rows from local SQLite to the cloud database.
/// Must run after <see cref="UserDrainer"/>. Maps local UserId → cloud UserId via Email.
/// Uses an upsert strategy keyed on cloud UserId.
/// </summary>
public sealed class PatientDrainer : ITableDrainer
{
    private static readonly TimeSpan PurgeOlderThan = TimeSpan.FromDays(7);

    private readonly IPatientRepository _local;
    private readonly IPatientRepository? _cloud;
    private readonly IUserRepository _localUser;
    private readonly IUserRepository? _cloudUser;
    private readonly ILogger<PatientDrainer> _logger;

    public int Order => 1;
    public string TableName => "Patients";

    public PatientDrainer(
        IPatientRepository local,
        IPatientRepository? cloud,
        IUserRepository localUser,
        IUserRepository? cloudUser,
        ILogger<PatientDrainer> logger)
    {
        _local = local;
        _cloud = cloud;
        _localUser = localUser;
        _cloudUser = cloudUser;
        _logger = logger;
    }

    public async Task<int> DrainAsync(CancellationToken ct = default)
    {
        if (_cloud is null || _cloudUser is null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("[{Table}] Cloud repository not configured — skipping.", TableName);
            return 0;
        }

        var dirty = (await _local.GetDirtyAsync()).ToList();
        if (dirty.Count == 0) return 0;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[{Table}] Draining {Count} dirty rows to cloud.", TableName, dirty.Count);

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
        var cloudUserId = await ResolveCloudUserIdAsync(patient.UserId);
        if (cloudUserId is null)
        {
            _logger.LogWarning("[{Table}] Cloud User not found for local UserId {UserId} — ensure UserDrainer runs first.", TableName, patient.UserId);
            return;
        }

        var existing = await _cloud!.GetByUserIdAsync(cloudUserId.Value);
        if (existing is not null)
        {
            existing.BloodType = patient.BloodType;
            existing.Allergies = patient.Allergies;
            existing.MedicalHistoryNotes = patient.MedicalHistoryNotes;
            await _cloud.UpdateAsync(existing);
        }
        else
        {
            var cloudPatient = new Patient
            {
                UserId = cloudUserId.Value,
                BloodType = patient.BloodType,
                Allergies = patient.Allergies,
                MedicalHistoryNotes = patient.MedicalHistoryNotes,
                CreatedAt = patient.CreatedAt,
                UpdatedAt = patient.UpdatedAt
            };
            await _cloud.AddAsync(cloudPatient);
        }
    }

    private async Task<Guid?> ResolveCloudUserIdAsync(Guid localUserId)
    {
        var localUser = await _localUser.GetByIdAsync(localUserId);
        if (localUser is null) return null;
        var cloudUser = await _cloudUser!.GetByEmailAsync(localUser.Email);
        return cloudUser?.Id;
    }
}
