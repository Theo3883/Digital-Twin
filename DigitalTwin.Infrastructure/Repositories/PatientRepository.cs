using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Repositories;

public class PatientRepository : IPatientRepository
{
    private readonly HealthAppDbContext _db;

    public PatientRepository(HealthAppDbContext db) => _db = db;

    public async Task<Patient?> GetByUserIdAsync(long userId)
    {
        var entity = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == userId);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(Patient patient)
    {
        var entity = ToEntity(patient);
        _db.Patients.Add(entity);
        await _db.SaveChangesAsync();
        patient.Id = entity.Id;
    }

    public async Task UpdateAsync(Patient patient)
    {
        var entity = await _db.Patients.FindAsync(patient.Id);
        if (entity is null) return;

        entity.BloodType = patient.BloodType;
        entity.Allergies = patient.Allergies;
        entity.MedicalHistoryNotes = patient.MedicalHistoryNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.IsDirty = true;
        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<Patient>> GetDirtyAsync()
    {
        var entities = await _db.Patients.Where(p => p.IsDirty).ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task MarkSyncedAsync(IEnumerable<Patient> items)
    {
        var ids = items.Select(p => p.Id).ToHashSet();
        await _db.Patients
            .Where(p => ids.Contains(p.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsDirty, false)
                .SetProperty(p => p.SyncedAt, DateTime.UtcNow));
    }

    public async Task PurgeSyncedOlderThanAsync(DateTime cutoffUtc)
    {
        await _db.Patients
            .Where(p => !p.IsDirty && p.SyncedAt.HasValue && p.SyncedAt.Value < cutoffUtc)
            .ExecuteDeleteAsync();
    }

    public async Task<bool> ExistsAsync(Patient patient)
    {
        return await _db.Patients.AnyAsync(p => p.UserId == patient.UserId);
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
        IsDirty = true,
        BloodType = model.BloodType,
        Allergies = model.Allergies,
        MedicalHistoryNotes = model.MedicalHistoryNotes,
        CreatedAt = model.CreatedAt,
        UpdatedAt = model.UpdatedAt
    };
}
