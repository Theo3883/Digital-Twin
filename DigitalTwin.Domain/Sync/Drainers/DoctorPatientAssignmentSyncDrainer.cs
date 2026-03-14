using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Interfaces.Sync;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Domain.Sync.Drainers;

/// <summary>
/// Bidirectional sync for <c>DoctorPatientAssignment</c> rows.
/// PUSH: dirty local assignments → cloud (maps local PatientId → cloud PatientId).
/// PULL: for each local patient, fetch cloud assignments and upsert locally.
/// Must run after <see cref="PatientSyncDrainer"/>.
/// </summary>
public sealed class DoctorPatientAssignmentSyncDrainer : SyncDrainerBase<DoctorPatientAssignment>
{
    private readonly IDoctorPatientAssignmentRepository _local;
    private readonly IDoctorPatientAssignmentRepository? _cloud;
    private readonly IPatientRepository _localPatient;
    private readonly ICloudIdentityResolver _identityResolver;

    public override int Order => 7;
    public override string TableName => "DoctorPatientAssignments";
    protected override bool IsCloudConfigured => _cloud is not null;

    public DoctorPatientAssignmentSyncDrainer(
        IDoctorPatientAssignmentRepository local,
        IDoctorPatientAssignmentRepository? cloud,
        IPatientRepository localPatient,
        ICloudIdentityResolver identityResolver,
        ILogger<DoctorPatientAssignmentSyncDrainer> logger) : base(logger)
    {
        _local = local;
        _cloud = cloud;
        _localPatient = localPatient;
        _identityResolver = identityResolver;
    }

    // ── Push hooks ───────────────────────────────────────────────────────────

    protected override async Task<List<DoctorPatientAssignment>> GetDirtyItemsAsync(CancellationToken ct)
        => (await _local.GetDirtyAsync()).ToList();

    protected override async Task<List<DoctorPatientAssignment>> MapToCloudBatchAsync(
        List<DoctorPatientAssignment> dirtyItems, CancellationToken ct)
    {
        var result = new List<DoctorPatientAssignment>();

        foreach (var assignment in dirtyItems)
        {
            ct.ThrowIfCancellationRequested();

            var cloudPatientId = await _identityResolver.ResolveCloudPatientIdAsync(assignment.PatientId, ct);
            if (cloudPatientId is null)
            {
                Logger.LogWarning("[{Table}] Cloud Patient not found for local PatientId {Id} — skipped.",
                    TableName, assignment.PatientId);
                continue;
            }

            result.Add(new DoctorPatientAssignment
            {
                Id = assignment.Id,
                DoctorId = assignment.DoctorId,
                PatientId = cloudPatientId.Value,
                PatientEmail = assignment.PatientEmail,
                AssignedByDoctorId = assignment.AssignedByDoctorId,
                Notes = assignment.Notes,
                AssignedAt = assignment.AssignedAt,
                CreatedAt = assignment.CreatedAt
            });
        }

        return result;
    }

    protected override async Task UpsertToCloudBatchAsync(List<DoctorPatientAssignment> cloudItems, CancellationToken ct)
    {
        foreach (var assignment in cloudItems)
        {
            ct.ThrowIfCancellationRequested();
            await _cloud!.AddAsync(assignment);
        }
    }

    protected override async Task MarkPushedAsSyncedAsync(List<DoctorPatientAssignment> originalDirtyItems, CancellationToken ct)
        => await _local.MarkSyncedAsync(originalDirtyItems);

    protected override async Task PurgeSyncedAsync(CancellationToken ct)
    {
        // DoctorPatientAssignments don't purge — they're lightweight reference data.
    }

    // ── Pull hooks ───────────────────────────────────────────────────────────

    protected override async Task<IReadOnlyList<PullScope>> GetPullScopesAsync(CancellationToken ct)
    {
        var localPatients = (await _localPatient.GetAllAsync()).ToList();
        var scopes = new List<PullScope>();

        foreach (var localPatient in localPatients)
        {
            ct.ThrowIfCancellationRequested();
            var cloudPatientId = await _identityResolver.ResolveCloudPatientIdAsync(localPatient.Id, ct);
            if (cloudPatientId is null) continue;
            scopes.Add(new PullScope(localPatient.Id, cloudPatientId.Value));
        }

        return scopes;
    }

    protected override async Task<IReadOnlyList<DoctorPatientAssignment>> FetchCloudItemsAsync(PullScope scope, CancellationToken ct)
        => (await _cloud!.GetByPatientIdAsync(scope.CloudId)).ToList();

    protected override async Task<int> MergeCloudItemsToLocalAsync(
        IReadOnlyList<DoctorPatientAssignment> cloudItems, PullScope scope, CancellationToken ct)
    {
        // Remap cloud PatientId → local PatientId for local cache consistency.
        var remapped = cloudItems.Select(a =>
        {
            a.PatientId = scope.LocalId;
            return a;
        }).ToList();

        await _local.UpsertRangeFromCloudAsync(scope.LocalId, remapped);
        return remapped.Count;
    }
}
