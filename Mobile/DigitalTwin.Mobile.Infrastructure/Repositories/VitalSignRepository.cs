using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Mobile.Infrastructure.Repositories;

/// <summary>
/// SQLite implementation of vital sign repository
/// </summary>
public class VitalSignRepository : IVitalSignRepository
{
    private readonly MobileDbContext _context;

    public VitalSignRepository(MobileDbContext context)
    {
        _context = context;
    }

    public async Task<VitalSign?> GetByIdAsync(Guid id)
    {
        return await _context.VitalSigns.FindAsync(id);
    }

    public async Task<IEnumerable<VitalSign>> GetByPatientIdAsync(Guid patientId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.VitalSigns.Where(v => v.PatientId == patientId);
        
        if (fromDate.HasValue)
            query = query.Where(v => v.Timestamp >= fromDate.Value);
        
        if (toDate.HasValue)
            query = query.Where(v => v.Timestamp <= toDate.Value);
        
        return await query.OrderByDescending(v => v.Timestamp).ToListAsync();
    }

    public async Task<IEnumerable<VitalSign>> GetByTypeAsync(Guid patientId, VitalSignType type, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.VitalSigns.Where(v => v.PatientId == patientId && v.Type == type);
        
        if (fromDate.HasValue)
            query = query.Where(v => v.Timestamp >= fromDate.Value);
        
        if (toDate.HasValue)
            query = query.Where(v => v.Timestamp <= toDate.Value);
        
        return await query.OrderByDescending(v => v.Timestamp).ToListAsync();
    }

    public async Task SaveAsync(VitalSign vitalSign)
    {
        var existing = await _context.VitalSigns.FindAsync(vitalSign.Id);
        if (existing == null)
        {
            _context.VitalSigns.Add(vitalSign);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(vitalSign);
        }
        
        await _context.SaveChangesAsync();
    }

    public async Task SaveRangeAsync(IEnumerable<VitalSign> vitalSigns)
    {
        foreach (var vitalSign in vitalSigns)
        {
            var existing = await _context.VitalSigns.FindAsync(vitalSign.Id);
            if (existing == null)
            {
                _context.VitalSigns.Add(vitalSign);
            }
            else
            {
                _context.Entry(existing).CurrentValues.SetValues(vitalSign);
            }
        }
        
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<VitalSign>> GetUnsyncedAsync()
    {
        return await _context.VitalSigns
            .Where(v => !v.IsSynced)
            .OrderBy(v => v.Timestamp)
            .ToListAsync();
    }

    public async Task MarkAsSyncedAsync(IEnumerable<Guid> ids)
    {
        var vitalSigns = await _context.VitalSigns
            .Where(v => ids.Contains(v.Id))
            .ToListAsync();
        
        foreach (var vital in vitalSigns)
        {
            vital.IsSynced = true;
        }
        
        await _context.SaveChangesAsync();
    }
}