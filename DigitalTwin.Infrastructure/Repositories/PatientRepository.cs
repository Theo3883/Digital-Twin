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
        await _db.SaveChangesAsync();
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
        UserId = model.UserId,
        BloodType = model.BloodType,
        Allergies = model.Allergies,
        MedicalHistoryNotes = model.MedicalHistoryNotes,
        CreatedAt = model.CreatedAt,
        UpdatedAt = model.UpdatedAt
    };
}
