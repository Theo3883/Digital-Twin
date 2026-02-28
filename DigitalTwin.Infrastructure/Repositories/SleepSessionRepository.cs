using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Repositories;

public class SleepSessionRepository : ISleepSessionRepository
{
    private readonly Func<HealthAppDbContext> _factory;
    private readonly bool _markDirtyOnInsert;

    public SleepSessionRepository(Func<HealthAppDbContext> factory, bool markDirtyOnInsert = true)
    {
        _factory = factory;
        _markDirtyOnInsert = markDirtyOnInsert;
    }

    public async Task<IEnumerable<SleepSession>> GetByPatientAsync(
        long patientId, DateTime? from = null, DateTime? to = null)
    {
        await using var db = _factory();
        var query = db.SleepSessions.Where(s => s.PatientId == patientId);

        if (from.HasValue) query = query.Where(s => s.StartTime >= from.Value);
        if (to.HasValue) query = query.Where(s => s.EndTime <= to.Value);

        var entities = await query.OrderByDescending(s => s.StartTime).ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task AddAsync(SleepSession session)
    {
        await using var db = _factory();
        var entity = ToEntity(session);
        entity.IsDirty = _markDirtyOnInsert;
        if (!_markDirtyOnInsert) entity.SyncedAt = DateTime.UtcNow;
        db.SleepSessions.Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<SleepSession> sessions)
    {
        var entities = sessions.Select(ToEntity).ToList();
        if (entities.Count == 0) return;
        foreach (var e in entities) e.IsDirty = _markDirtyOnInsert;
        if (!_markDirtyOnInsert) foreach (var e in entities) e.SyncedAt = DateTime.UtcNow;
        await using var db = _factory();
        await db.SleepSessions.AddRangeAsync(entities);
        await db.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(long patientId, DateTime startTime)
    {
        await using var db = _factory();
        return await db.SleepSessions
            .AnyAsync(s => s.PatientId == patientId && s.StartTime == startTime);
    }

    public async Task<IEnumerable<SleepSession>> GetDirtyAsync()
    {
        await using var db = _factory();
        var entities = await db.SleepSessions
            .Where(s => s.IsDirty)
            .OrderBy(s => s.StartTime)
            .ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task MarkSyncedAsync(long patientId, DateTime beforeTimestamp)
    {
        await using var db = _factory();
        await db.SleepSessions
            .Where(s => s.PatientId == patientId && s.IsDirty && s.StartTime <= beforeTimestamp)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsDirty, false)
                .SetProperty(x => x.SyncedAt, DateTime.UtcNow));
    }

    public async Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc)
    {
        await using var db = _factory();
        await db.SleepSessions
            .Where(s => !s.IsDirty && s.SyncedAt.HasValue && s.SyncedAt.Value < cutoffUtc)
            .ExecuteDeleteAsync();
    }

    private static SleepSession ToDomain(SleepSessionEntity e) => new()
    {
        PatientId = e.PatientId,
        StartTime = e.StartTime,
        EndTime = e.EndTime,
        DurationMinutes = e.DurationMinutes,
        QualityScore = (double)e.QualityScore
    };

    private static SleepSessionEntity ToEntity(SleepSession s) => new()
    {
        PatientId = s.PatientId,
        StartTime = s.StartTime,
        EndTime = s.EndTime,
        DurationMinutes = s.DurationMinutes,
        QualityScore = (decimal)s.QualityScore
    };
}
