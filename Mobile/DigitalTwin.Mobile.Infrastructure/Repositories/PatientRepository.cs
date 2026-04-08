using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Mobile.Infrastructure.Repositories;

/// <summary>
/// SQLite implementation of patient repository
/// </summary>
public class PatientRepository : IPatientRepository
{
    private readonly MobileDbContext _context;

    public PatientRepository(MobileDbContext context)
    {
        _context = context;
    }

    public async Task<Patient?> GetByIdAsync(Guid id)
    {
        return await _context.Patients.FindAsync(id);
    }

    public async Task<Patient?> GetByUserIdAsync(Guid userId)
    {
        return await _context.Patients.FirstOrDefaultAsync(p => p.UserId == userId);
    }

    public async Task<Patient?> GetCurrentPatientAsync()
    {
        // In mobile app, get patient for current user
        var currentUser = await _context.Users.FirstOrDefaultAsync();
        if (currentUser == null) return null;

        return await _context.Patients.FirstOrDefaultAsync(p => p.UserId == currentUser.Id);
    }

    public async Task SaveAsync(Patient patient)
    {
        var existing = await _context.Patients.FindAsync(patient.Id);
        if (existing == null)
        {
            _context.Patients.Add(patient);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(patient);
        }
        
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<Patient>> GetUnsyncedAsync()
    {
        return await _context.Patients
            .Where(p => !p.IsSynced)
            .ToListAsync();
    }

    public async Task MarkAsSyncedAsync(Guid id)
    {
        var patient = await _context.Patients.FindAsync(id);
        if (patient != null)
        {
            patient.IsSynced = true;
            await _context.SaveChangesAsync();
        }
    }
}