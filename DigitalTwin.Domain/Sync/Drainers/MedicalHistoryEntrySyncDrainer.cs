using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Sync;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Domain.Sync.Drainers;

/// <summary>
/// Sync drainer for structured OCR medical-history entries.
/// </summary>
public sealed class MedicalHistoryEntrySyncDrainer : SyncDrainerBase<MedicalHistoryEntry>
{
    private readonly IMedicalHistoryEntryRepository _local;
    private readonly IMedicalHistoryEntryRepository? _cloud;
    private readonly IPatientRepository _localPatient;
    private readonly ICloudIdentityResolver _identityResolver;

    public override int Order => 9;
    public override string TableName => "MedicalHistoryEntries";
    protected override bool IsCloudConfigured => _cloud is not null;

    public MedicalHistoryEntrySyncDrainer(
        IMedicalHistoryEntryRepository local,
        IMedicalHistoryEntryRepository? cloud,
        IPatientRepository localPatient,
        ICloudIdentityResolver identityResolver,
        ILogger<MedicalHistoryEntrySyncDrainer> logger) : base(logger)
    {
        _local = local;
        _cloud = cloud;
        _localPatient = localPatient;
        _identityResolver = identityResolver;
    }

    protected override async Task<List<MedicalHistoryEntry>> GetDirtyItemsAsync(CancellationToken ct)
        => (await _local.GetDirtyAsync()).ToList();

    protected override async Task<List<MedicalHistoryEntry>> MapToCloudBatchAsync(List<MedicalHistoryEntry> dirtyItems, CancellationToken ct)
    {
        var result = new List<MedicalHistoryEntry>();
        var cache = new Dictionary<Guid, Guid>();

        foreach (var row in dirtyItems)
        {
            if (!cache.TryGetValue(row.PatientId, out var cloudPatientId))
            {
                var resolved = await _identityResolver.ResolveCloudPatientIdAsync(row.PatientId, ct);
                if (resolved is null)
                    continue;
                cloudPatientId = resolved.Value;
                cache[row.PatientId] = cloudPatientId;
            }

            row.PatientId = cloudPatientId;
            result.Add(row);
        }

        return result;
    }

    protected override Task UpsertToCloudBatchAsync(List<MedicalHistoryEntry> cloudItems, CancellationToken ct)
        => _cloud!.UpsertRangeAsync(cloudItems);

    protected override Task MarkPushedAsSyncedAsync(List<MedicalHistoryEntry> originalDirtyItems, CancellationToken ct)
        => _local.MarkSyncedAsync(originalDirtyItems.Select(x => x.Id));

    protected override Task PurgeSyncedAsync(CancellationToken ct)
        => _local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);

    protected override async Task<IReadOnlyList<PullScope>> GetPullScopesAsync(CancellationToken ct)
    {
        var localPatients = await _localPatient.GetAllAsync();
        var scopes = new List<PullScope>();
        foreach (var p in localPatients)
        {
            var cloudId = await _identityResolver.ResolveCloudPatientIdAsync(p.Id, ct);
            if (cloudId is not null)
                scopes.Add(new PullScope(p.Id, cloudId.Value));
        }
        return scopes;
    }

    protected override async Task<IReadOnlyList<MedicalHistoryEntry>> FetchCloudItemsAsync(PullScope scope, CancellationToken ct)
        => (await _cloud!.GetByPatientAsync(scope.CloudId)).ToList();

    protected override async Task<int> MergeCloudItemsToLocalAsync(IReadOnlyList<MedicalHistoryEntry> cloudItems, PullScope scope, CancellationToken ct)
    {
        var existing = await _local.GetByPatientAsync(scope.LocalId);
        var existingIds = existing.Select(x => x.Id).ToHashSet();
        var toAdd = cloudItems
            .Where(x => !existingIds.Contains(x.Id))
            .Select(x => new MedicalHistoryEntry
            {
                Id = x.Id,
                PatientId = scope.LocalId,
                SourceDocumentId = x.SourceDocumentId,
                Title = x.Title,
                MedicationName = x.MedicationName,
                Dosage = x.Dosage,
                Frequency = x.Frequency,
                Duration = x.Duration,
                Notes = x.Notes,
                Summary = x.Summary,
                Confidence = x.Confidence,
                EventDate = x.EventDate,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                IsDirty = false
            })
            .ToList();

        if (toAdd.Count == 0) return 0;
        await _local.AddRangeAsync(toAdd);
        await _local.MarkSyncedAsync(toAdd.Select(x => x.Id));
        return toAdd.Count;
    }
}

