using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Repositories;

public class DoctorPatientAssignmentRepository : IDoctorPatientAssignmentRepository
{
    private readonly Func<HealthAppDbContext> _factory;
    private readonly bool _markDirtyOnInsert;

    public DoctorPatientAssignmentRepository(Func<HealthAppDbContext> factory, bool markDirtyOnInsert = true)
    {
        _factory = factory;
        _markDirtyOnInsert = markDirtyOnInsert;
    }

    public async Task<IEnumerable<DoctorPatientAssignment>> GetByDoctorIdAsync(Guid doctorId)
    {
        await using var db = _factory();
        var entities = await db.DoctorPatientAssignments
            .Where(a => a.DoctorId == doctorId)
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task<IEnumerable<DoctorPatientAssignment>> GetByPatientIdAsync(Guid patientId)
    {
        await using var db = _factory();
        var entities = await db.DoctorPatientAssignments
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task<IEnumerable<DoctorPatientAssignment>> GetByPatientEmailAsync(string patientEmail)
    {
        var normalized = (patientEmail ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)) return [];

        await using var db = _factory();
        var entities = await db.DoctorPatientAssignments
            .Where(a => string.Equals(a.PatientEmail, normalized, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task<DoctorPatientAssignment?> GetByDoctorAndPatientAsync(Guid doctorId, Guid patientId)
    {
        await using var db = _factory();
        var entity = await db.DoctorPatientAssignments
            .FirstOrDefaultAsync(a => a.DoctorId == doctorId && a.PatientId == patientId);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<bool> IsAssignedAsync(Guid doctorId, Guid patientId)
    {
        await using var db = _factory();
        return await db.DoctorPatientAssignments
            .AnyAsync(a => a.DoctorId == doctorId && a.PatientId == patientId);
    }

    public async Task AddAsync(DoctorPatientAssignment assignment)
    {
        await using var db = _factory();

        // A soft-deleted record for this pair may still exist in the DB, causing the
        // unique constraint to fire on a plain INSERT.  Reactivate it instead.
        var existing = await db.DoctorPatientAssignments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.DoctorId == assignment.DoctorId && a.PatientId == assignment.PatientId);

        if (existing is not null)
        {
            existing.DeletedAt = null;
            existing.AssignedAt = DateTime.UtcNow;
            existing.AssignedByDoctorId = assignment.AssignedByDoctorId;
            existing.Notes = assignment.Notes;
            existing.PatientEmail = assignment.PatientEmail;
            await db.SaveChangesAsync();
            assignment.Id = existing.Id;
            assignment.AssignedAt = existing.AssignedAt;
            return;
        }

        var entity = ToEntity(assignment);
        entity.IsDirty = _markDirtyOnInsert;
        db.DoctorPatientAssignments.Add(entity);
        await db.SaveChangesAsync();
        assignment.Id = entity.Id;
    }

    public async Task<IEnumerable<DoctorPatientAssignment>> GetDirtyAsync()
    {
        await using var db = _factory();
        var entities = await db.DoctorPatientAssignments
            .Where(a => a.IsDirty)
            .ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task MarkSyncedAsync(IEnumerable<DoctorPatientAssignment> assignments)
    {
        var ids = assignments.Select(a => a.Id).ToList();
        if (ids.Count == 0) return;

        await using var db = _factory();
        await db.DoctorPatientAssignments
            .Where(a => ids.Contains(a.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.IsDirty, false)
                .SetProperty(a => a.SyncedAt, DateTime.UtcNow));
    }

    public async Task UpsertRangeFromCloudAsync(Guid patientId, IEnumerable<DoctorPatientAssignment> cloudAssignments)
    {
        var cloud = cloudAssignments.ToList();
        await using var db = _factory();

        var existing = await db.DoctorPatientAssignments
            .IgnoreQueryFilters()
            .Where(a => a.PatientId == patientId)
            .ToListAsync();

        var cloudIds = cloud.Select(c => c.Id).ToHashSet();

        // Soft-delete local rows that are no longer present in the cloud set.
        foreach (var local in existing.Where(e => e.DeletedAt == null && !cloudIds.Contains(e.Id)))
            local.DeletedAt = DateTime.UtcNow;

        // Upsert each cloud assignment into the local cache.
        foreach (var c in cloud)
        {
            var row = existing.FirstOrDefault(e => e.Id == c.Id);
            if (row is null)
            {
                db.DoctorPatientAssignments.Add(new DoctorPatientAssignmentEntity
                {
                    Id                 = c.Id,
                    DoctorId           = c.DoctorId,
                    PatientId          = c.PatientId,
                    PatientEmail       = c.PatientEmail,
                    AssignedByDoctorId = c.AssignedByDoctorId,
                    Notes              = c.Notes,
                    AssignedAt         = c.AssignedAt,
                    CreatedAt          = c.CreatedAt,
                    IsDirty            = false,
                    SyncedAt           = DateTime.UtcNow
                });
            }
            else
            {
                row.DeletedAt          = null;
                row.AssignedAt         = c.AssignedAt;
                row.AssignedByDoctorId = c.AssignedByDoctorId;
                row.Notes              = c.Notes;
                row.PatientEmail       = c.PatientEmail;
                row.IsDirty            = false;
                row.SyncedAt           = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task RemoveAsync(Guid doctorId, Guid patientId)
    {
        await using var db = _factory();
        // Soft-delete via DeletedAt (query filter will exclude it)
        await db.DoctorPatientAssignments
            .Where(a => a.DoctorId == doctorId && a.PatientId == patientId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.DeletedAt, DateTime.UtcNow));
    }

    // ── Mappers ──────────────────────────────────────────────────────────────────

    private static DoctorPatientAssignment ToDomain(DoctorPatientAssignmentEntity e) => new()
    {
        Id = e.Id,
        DoctorId = e.DoctorId,
        PatientId = e.PatientId,
        PatientEmail = e.PatientEmail,
        AssignedAt = e.AssignedAt,
        AssignedByDoctorId = e.AssignedByDoctorId,
        Notes = e.Notes,
        CreatedAt = e.CreatedAt
    };

    private static DoctorPatientAssignmentEntity ToEntity(DoctorPatientAssignment m) => new()
    {
        Id                 = m.Id == Guid.Empty ? Guid.NewGuid() : m.Id,
        DoctorId           = m.DoctorId,
        PatientId          = m.PatientId,
        PatientEmail       = m.PatientEmail,
        AssignedAt         = m.AssignedAt,
        AssignedByDoctorId = m.AssignedByDoctorId,
        Notes              = m.Notes,
        CreatedAt          = m.CreatedAt
        // IsDirty set by callers that know the context (local vs. cloud)
    };
}
