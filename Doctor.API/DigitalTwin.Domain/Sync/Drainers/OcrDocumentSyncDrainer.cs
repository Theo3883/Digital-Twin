using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Sync;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Domain.Sync.Drainers;

/// <summary>
/// Bidirectional sync for <c>OcrDocument</c> rows.
/// PUSH: dirty local OCR documents → cloud (maps local PatientId → cloud PatientId).
/// PULL: for each local patient, fetch cloud OCR document metadata and add any missing locally.
/// Must run after <see cref="PatientSyncDrainer"/>.
/// Only sanitized metadata is synced — EncryptedVaultPath and raw OCR stay local.
/// </summary>
public sealed class OcrDocumentSyncDrainer : SyncDrainerBase<OcrDocument>
{
    private readonly IOcrDocumentRepository _local;
    private readonly IOcrDocumentRepository? _cloud;
    private readonly IPatientRepository _localPatient;
    private readonly ICloudIdentityResolver _identityResolver;

    public override int Order => 8;
    public override string TableName => "OcrDocuments";
    protected override bool IsCloudConfigured => _cloud is not null;

    public OcrDocumentSyncDrainer(
        IOcrDocumentRepository local,
        IOcrDocumentRepository? cloud,
        IPatientRepository localPatient,
        ICloudIdentityResolver identityResolver,
        ILogger<OcrDocumentSyncDrainer> logger) : base(logger)
    {
        _local = local;
        _cloud = cloud;
        _localPatient = localPatient;
        _identityResolver = identityResolver;
    }

    // ── Push hooks ────────────────────────────────────────────────────────────

    protected override async Task<List<OcrDocument>> GetDirtyItemsAsync(CancellationToken ct)
        => (await _local.GetDirtyAsync()).ToList();

    protected override async Task<List<OcrDocument>> MapToCloudBatchAsync(
        List<OcrDocument> dirtyItems, CancellationToken ct)
    {
        var result = new List<OcrDocument>();
        var cache = new Dictionary<Guid, Guid>();

        foreach (var doc in dirtyItems)
        {
            ct.ThrowIfCancellationRequested();

            if (!cache.TryGetValue(doc.PatientId, out var cloudPatientId))
            {
                var resolved = await _identityResolver.ResolveCloudPatientIdAsync(doc.PatientId, ct);
                if (resolved is null)
                {
                    Logger.LogWarning("[{Table}] Cloud Patient not found for local PatientId {Id} — skipped.", TableName, doc.PatientId);
                    continue;
                }
                cloudPatientId = resolved.Value;
                cache[doc.PatientId] = cloudPatientId;
            }

            result.Add(new OcrDocument
            {
                Id = doc.Id,
                PatientId = cloudPatientId,
                OpaqueInternalName = doc.OpaqueInternalName,
                MimeType = doc.MimeType,
                PageCount = doc.PageCount,
                Sha256OfNormalized = doc.Sha256OfNormalized,
                SanitizedOcrPreview = doc.SanitizedOcrPreview,
                // EncryptedVaultPath intentionally omitted — local-only
                EncryptedVaultPath = string.Empty,
                ScannedAt = doc.ScannedAt,
                CreatedAt = doc.CreatedAt,
                UpdatedAt = doc.UpdatedAt
            });
        }

        return result;
    }

    protected override async Task UpsertToCloudBatchAsync(List<OcrDocument> cloudItems, CancellationToken ct)
        => await _cloud!.UpsertRangeAsync(cloudItems);

    protected override async Task MarkPushedAsSyncedAsync(List<OcrDocument> originalDirtyItems, CancellationToken ct)
    {
        foreach (var doc in originalDirtyItems)
            await _local.MarkSyncedAsync(doc.Id);
    }

    protected override async Task PurgeSyncedAsync(CancellationToken ct)
        => await _local.PurgeSyncedOlderThanAsync(DateTime.UtcNow - PurgeOlderThan);

    // ── Pull hooks ────────────────────────────────────────────────────────────

    protected override async Task<IReadOnlyList<PullScope>> GetPullScopesAsync(CancellationToken ct)
    {
        var localPatients = await _localPatient.GetAllAsync();
        var scopes = new List<PullScope>();

        foreach (var patient in localPatients)
        {
            ct.ThrowIfCancellationRequested();
            var cloudPatientId = await _identityResolver.ResolveCloudPatientIdAsync(patient.Id, ct);
            if (cloudPatientId is null) continue;
            scopes.Add(new PullScope(patient.Id, cloudPatientId.Value));
        }

        return scopes;
    }

    protected override async Task<IReadOnlyList<OcrDocument>> FetchCloudItemsAsync(
        PullScope scope, CancellationToken ct)
        => (await _cloud!.GetByPatientAsync(scope.CloudId)).ToList();

    protected override async Task<int> MergeCloudItemsToLocalAsync(
        IReadOnlyList<OcrDocument> cloudItems, PullScope scope, CancellationToken ct)
    {
        var existing = await _local.GetByPatientAsync(scope.LocalId);
        var existingIds = existing.Select(d => d.Id).ToHashSet();

        var toAdd = cloudItems
            .Where(d => !existingIds.Contains(d.Id))
            .Select(d => new OcrDocument
            {
                Id = d.Id,
                PatientId = scope.LocalId,
                OpaqueInternalName = d.OpaqueInternalName,
                MimeType = d.MimeType,
                PageCount = d.PageCount,
                Sha256OfNormalized = d.Sha256OfNormalized,
                SanitizedOcrPreview = d.SanitizedOcrPreview,
                EncryptedVaultPath = string.Empty, // no vault on pull — metadata only
                ScannedAt = d.ScannedAt,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt,
                IsDirty = false
            })
            .ToList();

        if (toAdd.Count == 0) return 0;

        foreach (var doc in toAdd)
            await _local.AddAsync(doc);

        return toAdd.Count;
    }
}
