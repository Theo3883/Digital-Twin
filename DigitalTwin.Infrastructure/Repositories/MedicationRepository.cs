using DigitalTwin.Domain.Enums;
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
public class MedicationRepository : IMedicationRepository
{
    private readonly Func<HealthAppDbContext> _factory;
    private readonly bool _markDirtyOnInsert;

    public MedicationRepository(Func<HealthAppDbContext> factory, bool markDirtyOnInsert = true)
    {
        _factory = factory;
        _markDirtyOnInsert = markDirtyOnInsert;
    }

    public async Task<IEnumerable<Medication>> GetByPatientAsync(Guid patientId)
    {
        await using var db = _factory();
        var entities = await db.Medications
            .Where(m => m.PatientId == patientId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task<Medication?> GetByIdAsync(Guid id)
    {
        await using var db = _factory();
        var entity = await db.Medications.FirstOrDefaultAsync(m => m.Id == id);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(Medication medication)
    {
        await using var db = _factory();
        var entity = ToEntity(medication);
        entity.IsDirty = _markDirtyOnInsert;
        if (!_markDirtyOnInsert) entity.SyncedAt = DateTime.UtcNow;
        db.Medications.Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<Medication> medications)
    {
        var entities = medications.Select(ToEntity).ToList();
        if (entities.Count == 0) return;
        foreach (var e in entities) e.IsDirty = _markDirtyOnInsert;
        if (!_markDirtyOnInsert) foreach (var e in entities) e.SyncedAt = DateTime.UtcNow;
        await using var db = _factory();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            await db.Medications.AddRangeAsync(entities);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task SoftDeleteAsync(Guid id)
    {
        await using var db = _factory();
        await db.Medications
            .Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.DeletedAt, DateTime.UtcNow)
                .SetProperty(m => m.UpdatedAt, DateTime.UtcNow));
    }

    public async Task DiscontinueAsync(Guid id, DateTime endDate, string? reason)
    {
        await using var db = _factory();
        await db.Medications
            .Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, (int)MedicationStatus.Discontinued)
                .SetProperty(m => m.EndDate, endDate)
                .SetProperty(m => m.DiscontinuedReason, reason)
                .SetProperty(m => m.UpdatedAt, DateTime.UtcNow)
                .SetProperty(m => m.IsDirty, true));
    }

    public async Task<IEnumerable<Medication>> GetDirtyAsync()
    {
        await using var db = _factory();
        var entities = await db.Medications
            .IgnoreQueryFilters()
            .Where(m => m.IsDirty && m.DeletedAt == null)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task UpdateAsync(Medication medication)
    {
        await using var db = _factory();
        var entity = await db.Medications.FirstOrDefaultAsync(m => m.Id == medication.Id);
        if (entity is null) return;

        entity.Name = medication.Name;
        entity.Dosage = medication.Dosage;
        entity.Frequency = medication.Frequency;
        entity.Route = (int)medication.Route;
        entity.RxCui = medication.RxCui;
        entity.Instructions = medication.Instructions;
        entity.Reason = medication.Reason;
        entity.PrescribedByUserId = medication.PrescribedByUserId;
        entity.StartDate = medication.StartDate;
        entity.EndDate = medication.EndDate;
        entity.Status = (int)medication.Status;
        entity.DiscontinuedReason = medication.DiscontinuedReason;
        entity.AddedByRole = (int)medication.AddedByRole;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.IsDirty = _markDirtyOnInsert;

        await db.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        await using var db = _factory();
        return await db.Medications.AnyAsync(m => m.Id == id);
    }

    public async Task MarkSyncedAsync(Guid patientId)
    {
        await using var db = _factory();
        await db.Medications
            .IgnoreQueryFilters()
            .Where(m => m.PatientId == patientId && m.IsDirty && m.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.IsDirty, false)
                .SetProperty(m => m.SyncedAt, DateTime.UtcNow));
    }

    public async Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc)
    {
        await using var db = _factory();
        await db.Medications
            .IgnoreQueryFilters()
            .Where(m => !m.IsDirty && m.SyncedAt != null && m.SyncedAt < cutoffUtc)
            .ExecuteDeleteAsync();
    }

    private static Medication ToDomain(MedicationEntity e) => new()
    {
        Id = e.Id,
        PatientId = e.PatientId,
        Name = e.Name,
        Dosage = e.Dosage,
        Frequency = e.Frequency,
        Route = (MedicationRoute)e.Route,
        RxCui = e.RxCui,
        Instructions = e.Instructions,
        Reason = e.Reason,
        PrescribedByUserId = e.PrescribedByUserId,
        StartDate = e.StartDate,
        EndDate = e.EndDate,
        Status = (MedicationStatus)e.Status,
        DiscontinuedReason = e.DiscontinuedReason,
        AddedByRole = (AddedByRole)e.AddedByRole,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };

    private static MedicationEntity ToEntity(Medication m) => new()
    {
        Id = m.Id,
        PatientId = m.PatientId,
        Name = m.Name,
        Dosage = m.Dosage,
        Frequency = m.Frequency,
        Route = (int)m.Route,
        RxCui = m.RxCui,
        Instructions = m.Instructions,
        Reason = m.Reason,
        PrescribedByUserId = m.PrescribedByUserId,
        StartDate = m.StartDate,
        EndDate = m.EndDate,
        Status = (int)m.Status,
        DiscontinuedReason = m.DiscontinuedReason,
        AddedByRole = (int)m.AddedByRole,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };
}
