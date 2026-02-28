using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Repositories;

public class DoctorPatientAssignmentRepository : IDoctorPatientAssignmentRepository
{
    private readonly Func<HealthAppDbContext> _factory;

    public DoctorPatientAssignmentRepository(Func<HealthAppDbContext> factory)
    {
        _factory = factory;
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
        var entity = ToEntity(assignment);
        db.DoctorPatientAssignments.Add(entity);
        await db.SaveChangesAsync();
        assignment.Id = entity.Id;
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
        Id = m.Id == Guid.Empty ? Guid.NewGuid() : m.Id,
        DoctorId = m.DoctorId,
        PatientId = m.PatientId,
        PatientEmail = m.PatientEmail,
        AssignedAt = m.AssignedAt,
        AssignedByDoctorId = m.AssignedByDoctorId,
        Notes = m.Notes,
        CreatedAt = m.CreatedAt
    };
}
