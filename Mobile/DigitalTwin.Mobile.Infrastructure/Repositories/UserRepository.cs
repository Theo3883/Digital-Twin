using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using DigitalTwin.Mobile.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Mobile.Infrastructure.Repositories;

/// <summary>
/// SQLite implementation of user repository
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly MobileDbContext _context;

    public UserRepository(MobileDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        // In mobile app, there's typically one user at a time
        return await _context.Users.FirstOrDefaultAsync();
    }

    public async Task SaveAsync(User user)
    {
        var existing = await _context.Users.FindAsync(user.Id);
        if (existing == null)
        {
            _context.Users.Add(user);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(user);
        }
        
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<User>> GetUnsyncedAsync()
    {
        return await _context.Users
            .Where(u => !u.IsSynced)
            .ToListAsync();
    }

    public async Task MarkAsSyncedAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            user.IsSynced = true;
            await _context.SaveChangesAsync();
        }
    }
}