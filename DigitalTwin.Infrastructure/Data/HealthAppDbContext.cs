using DigitalTwin.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Data;

public class HealthAppDbContext : DbContext
{
    public HealthAppDbContext(DbContextOptions<HealthAppDbContext> options) : base(options) { }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<UserOAuthEntity> UserOAuths => Set<UserOAuthEntity>();
    public DbSet<PatientEntity> Patients => Set<PatientEntity>();
    public DbSet<DoctorPatientAssignmentEntity> DoctorPatientAssignments => Set<DoctorPatientAssignmentEntity>();
    public DbSet<VitalSignEntity> VitalSigns => Set<VitalSignEntity>();
    public DbSet<MedicationEntity> Medications => Set<MedicationEntity>();
    public DbSet<EnvironmentReadingEntity> EnvironmentReadings => Set<EnvironmentReadingEntity>();
    public DbSet<SleepSessionEntity> SleepSessions => Set<SleepSessionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserEntity>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.HasOne(u => u.Patient).WithOne(p => p.User).HasForeignKey<PatientEntity>(p => p.UserId);
            e.HasMany(u => u.OAuthAccounts).WithOne(o => o.User).HasForeignKey(o => o.UserId);
            e.HasQueryFilter(u => u.DeletedAt == null);
        });

        modelBuilder.Entity<UserOAuthEntity>(e =>
        {
            e.HasKey(o => o.Id);
            e.HasQueryFilter(o => o.DeletedAt == null);
        });

        modelBuilder.Entity<PatientEntity>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.UserId).IsUnique();
            e.HasMany(p => p.VitalSigns).WithOne(v => v.Patient).HasForeignKey(v => v.PatientId);
            e.HasMany(p => p.Medications).WithOne(m => m.Patient).HasForeignKey(m => m.PatientId);
            e.HasMany(p => p.SleepSessions).WithOne(s => s.Patient).HasForeignKey(s => s.PatientId);
            e.HasQueryFilter(p => p.DeletedAt == null);
        });

        modelBuilder.Entity<DoctorPatientAssignmentEntity>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => new { d.DoctorId, d.PatientId }).IsUnique();
            e.HasQueryFilter(d => d.DeletedAt == null);
        });

        modelBuilder.Entity<VitalSignEntity>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.Value).HasPrecision(18, 4);
            e.HasQueryFilter(v => v.DeletedAt == null);
        });

        modelBuilder.Entity<MedicationEntity>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasQueryFilter(m => m.DeletedAt == null);
        });

        modelBuilder.Entity<EnvironmentReadingEntity>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Latitude).HasPrecision(10, 7);
            e.Property(r => r.Longitude).HasPrecision(10, 7);
            e.Property(r => r.PM25).HasPrecision(10, 2);
            e.Property(r => r.Temperature).HasPrecision(5, 2);
            e.Property(r => r.Humidity).HasPrecision(5, 2);
            e.HasQueryFilter(r => r.DeletedAt == null);
        });

        modelBuilder.Entity<SleepSessionEntity>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.QualityScore).HasPrecision(5, 2);
            e.HasQueryFilter(s => s.DeletedAt == null);
        });
    }
}
