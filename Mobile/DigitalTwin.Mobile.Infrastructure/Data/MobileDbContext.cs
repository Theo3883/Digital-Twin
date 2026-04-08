using DigitalTwin.Mobile.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Mobile.Infrastructure.Data;

/// <summary>
/// SQLite database context for mobile app
/// </summary>
public class MobileDbContext : DbContext
{
    public MobileDbContext(DbContextOptions<MobileDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Patient> Patients { get; set; } = null!;
    public DbSet<VitalSign> VitalSigns { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.PhotoUrl).HasMaxLength(500);
            entity.Property(e => e.Phone).HasMaxLength(20);
        });

        // Patient configuration
        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.BloodType).HasMaxLength(10);
            entity.Property(e => e.Allergies).HasMaxLength(1000);
            entity.Property(e => e.MedicalHistoryNotes).HasMaxLength(2000);
            entity.Property(e => e.Cnp).HasMaxLength(20);
        });

        // VitalSign configuration
        modelBuilder.Entity<VitalSign>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.PatientId, e.Type, e.Timestamp });
            entity.Property(e => e.Unit).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(50);
        });

        base.OnModelCreating(modelBuilder);
    }
}