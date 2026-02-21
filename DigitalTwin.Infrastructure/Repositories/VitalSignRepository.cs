using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Repositories;

public class VitalSignRepository : IVitalSignRepository
{
    private readonly HealthAppDbContext _db;

    public VitalSignRepository(HealthAppDbContext db) => _db = db;

    public async Task<IEnumerable<VitalSign>> GetByPatientAsync(
        long patientId,
        VitalSignType? type = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var query = _db.VitalSigns.Where(v => v.PatientId == patientId);

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
        var entity = ToEntity(vitalSign);
        _db.VitalSigns.Add(entity);
        await _db.SaveChangesAsync();
    }

    private static VitalSign ToDomain(VitalSignEntity entity) => new()
    {
        Type = (VitalSignType)entity.Type,
        Value = (double)entity.Value,
        Unit = entity.Unit,
        Timestamp = entity.Timestamp
    };

    private static VitalSignEntity ToEntity(VitalSign model) => new()
    {
        Type = (int)model.Type,
        Value = (decimal)model.Value,
        Unit = model.Unit,
        Timestamp = model.Timestamp
    };
}
