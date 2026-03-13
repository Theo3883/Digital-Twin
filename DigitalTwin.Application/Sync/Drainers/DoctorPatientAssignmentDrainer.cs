using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Sync.Drainers;

/// <summary>
/// Bidirectional sync for <c>DoctorPatientAssignments</c>:
///
///   PUSH (local SQLite → cloud):
///     Locally-created assignments marked IsDirty are upserted to the cloud DB.
///
///   PULL (cloud → local SQLite):
///     For every Patient known locally, fetch the authoritative assignment list
///     from the cloud and upsert it into the local SQLite cache, including
///     reactivating soft-deleted rows and soft-deleting rows removed on cloud.
///
/// Must run after <see cref="PatientDrainer"/> (Order=1) so cloud Patient IDs exist.
/// </summary>
public sealed class DoctorPatientAssignmentDrainer : ITableDrainer
{
    private readonly IDoctorPatientAssignmentRepository _local;
    private readonly IDoctorPatientAssignmentRepository? _cloud;
    private readonly IPatientRepository _localPatient;
    private readonly IPatientRepository? _cloudPatient;
    private readonly ILogger<DoctorPatientAssignmentDrainer> _logger;

    public int Order => 6;
    public string TableName => "DoctorPatientAssignments";

    public DoctorPatientAssignmentDrainer(
        IDoctorPatientAssignmentRepository local,
        IDoctorPatientAssignmentRepository? cloud,
        IPatientRepository localPatient,
        IPatientRepository? cloudPatient,
        ILogger<DoctorPatientAssignmentDrainer> logger)
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

    // ── PUSH: local dirty → cloud ────────────────────────────────────────────

    private async Task<int> PushAsync(CancellationToken ct)
    {
        var dirty = (await _local.GetDirtyAsync()).ToList();
        if (dirty.Count == 0) return 0;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[{Table}] Pushing {Count} dirty local assignments to cloud.", TableName, dirty.Count);

        var pushed = new List<DoctorPatientAssignment>();

        foreach (var assignment in dirty)
        {
            ct.ThrowIfCancellationRequested();

            // Resolve local PatientId → cloud PatientId via the local patient's UserId.
            var localPatient = await _localPatient.GetByIdAsync(assignment.PatientId);
            if (localPatient is null)
            {
                _logger.LogWarning("[{Table}] Local Patient {Id} not found — assignment skipped.", TableName, assignment.PatientId);
                continue;
            }

            var cloudPatient = await _cloudPatient!.GetByUserIdAsync(localPatient.UserId);
            if (cloudPatient is null)
            {
                _logger.LogWarning("[{Table}] Cloud Patient for UserId {UserId} not found — ensure PatientDrainer runs first.", TableName, localPatient.UserId);
                continue;
            }

            var cloudAssignment = new DoctorPatientAssignment
            {
                Id                 = assignment.Id,
                DoctorId           = assignment.DoctorId,
                PatientId          = cloudPatient.Id,
                PatientEmail       = assignment.PatientEmail,
                AssignedByDoctorId = assignment.AssignedByDoctorId,
                Notes              = assignment.Notes,
                AssignedAt         = assignment.AssignedAt,
                CreatedAt          = assignment.CreatedAt
            };

            await _cloud!.AddAsync(cloudAssignment);
            pushed.Add(assignment);
        }

        if (pushed.Count > 0)
            await _local.MarkSyncedAsync(pushed);

        return pushed.Count;
    }

    // ── PULL: cloud → local SQLite ───────────────────────────────────────────

    private async Task<int> PullAsync(CancellationToken ct)
    {
        var localPatients = (await _localPatient.GetAllAsync()).ToList();
        if (localPatients.Count == 0) return 0;

        int total = 0;

        foreach (var localPatient in localPatients)
        {
            ct.ThrowIfCancellationRequested();

            var cloudPatient = await _cloudPatient!.GetByUserIdAsync(localPatient.UserId);
            if (cloudPatient is null) continue;

            var cloudAssignments = (await _cloud!.GetByPatientIdAsync(cloudPatient.Id)).ToList();

            // Re-map cloud PatientId → local PatientId so the local cache is self-consistent.
            foreach (var a in cloudAssignments)
                a.PatientId = localPatient.Id;

            await _local.UpsertRangeFromCloudAsync(localPatient.Id, cloudAssignments);
            total += cloudAssignments.Count;

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("[{Table}] Pulled {Count} assignments for local patient {PatientId}.",
                    TableName, cloudAssignments.Count, localPatient.Id);
        }

        return total;
    }
}
