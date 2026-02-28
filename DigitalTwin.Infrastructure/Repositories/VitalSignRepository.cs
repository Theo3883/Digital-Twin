using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Repositories;

/// <summary>
/// Each method creates and disposes its own DbContext via the factory, so concurrent
/// callers from different threads never share a DbContext instance.
/// </summary>
public class VitalSignRepository : IVitalSignRepository
{
    // Func returns a BRAND NEW context every call — callers own lifetime via `await using`.
    private readonly Func<HealthAppDbContext> _factory;
    private readonly bool _markDirtyOnInsert;

    public VitalSignRepository(Func<HealthAppDbContext> factory, bool markDirtyOnInsert = true)
    {
        _factory = factory;
        _markDirtyOnInsert = markDirtyOnInsert;
    }

    public async Task<IEnumerable<VitalSign>> GetByPatientAsync(
        long patientId,
        VitalSignType? type = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        await using var db = _factory();
        var query = db.VitalSigns.Where(v => v.PatientId == patientId);

        if (type.HasValue)
            query = query.Where(v => v.Type == (int)type.Value);
        if (from.HasValue)
            query = query.Where(v => v.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(v => v.Timestamp <= to.Value);

        var entities = await query.OrderByDescending(v => v.Timestamp).ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task AddAsync(VitalSign vitalSign)
    {
        await using var db = _factory();
        var entity = ToEntity(vitalSign);
        entity.IsDirty = _markDirtyOnInsert;
        if (!_markDirtyOnInsert) entity.SyncedAt = DateTime.UtcNow;
        db.VitalSigns.Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<VitalSign> vitalSigns)
    {
        var entities = vitalSigns.Select(ToEntity).ToList();
        if (entities.Count == 0) return;
        foreach (var e in entities) e.IsDirty = _markDirtyOnInsert;
        if (!_markDirtyOnInsert) foreach (var e in entities) e.SyncedAt = DateTime.UtcNow;
        await using var db = _factory();
        await db.VitalSigns.AddRangeAsync(entities);
        await db.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(long patientId, VitalSignType type, DateTime timestamp)
    {
        await using var db = _factory();
        return await db.VitalSigns
            .AnyAsync(v => v.PatientId == patientId && v.Type == (int)type && v.Timestamp == timestamp);
    }

    public async Task<IEnumerable<VitalSign>> GetDirtyAsync()
    {
        await using var db = _factory();
        var entities = await db.VitalSigns
            .Where(v => v.IsDirty)
            .OrderBy(v => v.Timestamp)
            .ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task MarkSyncedAsync(long patientId, DateTime beforeTimestamp)
    {
        await using var db = _factory();
        await db.VitalSigns
            .Where(v => v.PatientId == patientId && v.IsDirty && v.Timestamp <= beforeTimestamp)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.IsDirty, false)
                .SetProperty(v => v.SyncedAt, DateTime.UtcNow));
    }

    public async Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc)
    {
        await using var db = _factory();
        await db.VitalSigns
            .Where(v => !v.IsDirty && v.SyncedAt.HasValue && v.SyncedAt.Value < cutoffUtc)
            .ExecuteDeleteAsync();
    }

    private static VitalSign ToDomain(VitalSignEntity entity) => new()
    {
        PatientId = entity.PatientId,
        Type = (VitalSignType)entity.Type,
        Value = (double)entity.Value,
        Unit = entity.Unit,
        Source = entity.Source ?? string.Empty,
        Timestamp = entity.Timestamp
    };

    private static VitalSignEntity ToEntity(VitalSign model) => new()
    {
        PatientId = model.PatientId,
        Type = (int)model.Type,
        Value = (decimal)model.Value,
        Unit = model.Unit,
        Source = model.Source,
        Timestamp = model.Timestamp,
    };
}
