using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync.Drainers;

/// <summary>
/// Pushes dirty local medications to cloud.
/// Must run after <see cref="PatientDrainer"/> so cloud Patient records exist.
/// </summary>
public sealed class MedicationDrainer(
    IMedicationRepository local,
    IMedicationRepository? cloud,
    IPatientRepository patient,
    IPatientRepository? cloudPatient,
    ICloudUserIdResolver cloudUserIdResolver,
    ILogger<MedicationDrainer> logger)
    : ITableDrainer
{
    public int Order => 4;
    public string TableName => "Medications";

    public async Task<int> DrainAsync(CancellationToken ct = default)
    {
        if (cloud is null || cloudPatient is null)
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("[{Table}] Cloud repository not configured — skipping.", TableName);
            return 0;
        }

        return await PushAsync(ct);
    }

    private async Task<int> PushAsync(CancellationToken ct)
    {
        var dirty = (await local.GetDirtyAsync()).ToList();
        if (dirty.Count == 0) return 0;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("[{Table}] Pushing {Count} dirty rows to cloud.", TableName, dirty.Count);

        var mapped = await MapToCloudPatientIdsAsync(dirty, ct);
        if (mapped.Count == 0)
        {
            logger.LogWarning("[{Table}] No medications could be mapped to cloud Patient — skipping (will retry).", TableName);
            return 0;
        }

        foreach (var med in mapped)
        {
            ct.ThrowIfCancellationRequested();
            await cloud!.AddAsync(med);
        }

        foreach (var group in dirty.GroupBy(m => m.PatientId))
            await local.MarkSyncedAsync(group.Key);

        return dirty.Count;
    }

    private async Task<List<Medication>> MapToCloudPatientIdsAsync(List<Medication> medications, CancellationToken ct)
    {
        var result = new List<Medication>();
        var localToCloud = new Dictionary<Guid, Guid>();

        foreach (var med in medications)
        {
            ct.ThrowIfCancellationRequested();

            if (!localToCloud.TryGetValue(med.PatientId, out var cloudPatientId))
            {
                var localPatient = await patient.GetByIdAsync(med.PatientId);
                if (localPatient is null)
                {
                    logger.LogWarning("[{Table}] Local Patient {Id} not found — medication skipped.", TableName, med.PatientId);
                    continue;
                }

                var cloudUserId = await cloudUserIdResolver.ResolveCloudUserIdAsync(localPatient.UserId, ct);
                if (cloudUserId is null)
                {
                    logger.LogWarning("[{Table}] Cloud User for local UserId {UserId} not found — ensure UserDrainer runs first.", TableName, localPatient.UserId);
                    continue;
                }
                var cloudP = await cloudPatient!.GetByUserIdAsync(cloudUserId.Value);
                if (cloudP is null)
                {
                    logger.LogWarning("[{Table}] Cloud Patient for UserId {UserId} not found — ensure PatientDrainer runs first.", TableName, cloudUserId.Value);
                    continue;
                }

                cloudPatientId = cloudP.Id;
                localToCloud[med.PatientId] = cloudPatientId;
            }

            result.Add(new Medication
            {
                Id = med.Id,
                PatientId = cloudPatientId,
                Name = med.Name,
                Dosage = med.Dosage,
                Frequency = med.Frequency,
                Route = med.Route,
                RxCui = med.RxCui,
                Instructions = med.Instructions,
                Reason = med.Reason,
                PrescribedByUserId = med.PrescribedByUserId,
                StartDate = med.StartDate,
                EndDate = med.EndDate,
                Status = med.Status,
                DiscontinuedReason = med.DiscontinuedReason,
                AddedByRole = med.AddedByRole,
                CreatedAt = med.CreatedAt,
                UpdatedAt = med.UpdatedAt
            });
        }

        return result;
    }
}
