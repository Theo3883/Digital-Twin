using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Repositories;

public class PatientRepository : IPatientRepository
{
    private readonly Func<HealthAppDbContext> _factory;
    private readonly bool _markDirtyOnInsert;

    public PatientRepository(Func<HealthAppDbContext> factory, bool markDirtyOnInsert = true)
    {
        _factory = factory;
        _markDirtyOnInsert = markDirtyOnInsert;
    }

    public async Task<Patient?> GetByIdAsync(long id)
    {
        await using var db = _factory();
        var entity = await db.Patients.FindAsync(id);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<Patient?> GetByUserIdAsync(long userId)
    {
        await using var db = _factory();
        var entity = await db.Patients.FirstOrDefaultAsync(p => p.UserId == userId);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(Patient patient)
    {
        await using var db = _factory();
        var entity = ToEntity(patient);
        entity.IsDirty = _markDirtyOnInsert;
        if (!_markDirtyOnInsert) entity.SyncedAt = DateTime.UtcNow;
        db.Patients.Add(entity);
        await db.SaveChangesAsync();
        patient.Id = entity.Id;
    }

    public async Task UpdateAsync(Patient patient)
    {
        await using var db = _factory();
        var entity = await db.Patients.FindAsync(patient.Id);
        if (entity is null) return;

        entity.BloodType = patient.BloodType;
        entity.Allergies = patient.Allergies;
        entity.MedicalHistoryNotes = patient.MedicalHistoryNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.IsDirty = _markDirtyOnInsert;
        if (!_markDirtyOnInsert) entity.SyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<Patient>> GetDirtyAsync()
    {
        await using var db = _factory();
        var entities = await db.Patients.Where(p => p.IsDirty).ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task MarkSyncedAsync(IEnumerable<Patient> items)
    {
        await using var db = _factory();
        var ids = items.Select(p => p.Id).ToHashSet();
        await db.Patients
            .Where(p => ids.Contains(p.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsDirty, false)
                .SetProperty(p => p.SyncedAt, DateTime.UtcNow));
    }

    public async Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc)
    {
        await using var db = _factory();
        await db.Patients
            .Where(p => !p.IsDirty && p.SyncedAt.HasValue && p.SyncedAt.Value < cutoffUtc)
            .ExecuteDeleteAsync();
    }

    public async Task<bool> ExistsAsync(Patient patient)
    {
        await using var db = _factory();
        return await db.Patients.AnyAsync(p => p.UserId == patient.UserId);
    }

    private static Patient ToDomain(PatientEntity entity) => new()
    {
        Id = entity.Id,
        UserId = entity.UserId,
        BloodType = entity.BloodType,
        Allergies = entity.Allergies,
        MedicalHistoryNotes = entity.MedicalHistoryNotes,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static PatientEntity ToEntity(Patient model) => new()
    {
        Id = model.Id,
        UserId = model.UserId,
        BloodType = model.BloodType,
        Allergies = model.Allergies,
        MedicalHistoryNotes = model.MedicalHistoryNotes,
        CreatedAt = model.CreatedAt,
        UpdatedAt = model.UpdatedAt
    };
}
