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
    public DbSet<Medication> Medications { get; set; } = null!;
    public DbSet<EnvironmentReading> EnvironmentReadings { get; set; } = null!;
    public DbSet<SleepSession> SleepSessions { get; set; } = null!;
    public DbSet<OcrDocument> OcrDocuments { get; set; } = null!;
    public DbSet<MedicalHistoryEntry> MedicalHistoryEntries { get; set; } = null!;
    public DbSet<ChatMessage> ChatMessages { get; set; } = null!;

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
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.Country).HasMaxLength(100);
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

        // Medication configuration
        modelBuilder.Entity<Medication>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.PatientId, e.Status });
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Dosage).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Frequency).HasMaxLength(100);
            entity.Property(e => e.RxCui).HasMaxLength(20);
            entity.Property(e => e.Instructions).HasMaxLength(1000);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.DiscontinuedReason).HasMaxLength(500);
        });

        // EnvironmentReading configuration
        modelBuilder.Entity<EnvironmentReading>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.Property(e => e.LocationDisplayName).HasMaxLength(200);
        });

        // SleepSession configuration
        modelBuilder.Entity<SleepSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.PatientId, e.StartTime });
        });

        // OcrDocument configuration
        modelBuilder.Entity<OcrDocument>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.PatientId, e.IsDirty });
            entity.Property(e => e.OpaqueInternalName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.MimeType).HasMaxLength(50);
            entity.Property(e => e.Sha256OfNormalized).HasMaxLength(64);
            entity.Property(e => e.SanitizedOcrPreview).HasMaxLength(4000);
            entity.Property(e => e.EncryptedVaultPath).HasMaxLength(500);
        });

        // MedicalHistoryEntry configuration
        modelBuilder.Entity<MedicalHistoryEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PatientId);
            entity.HasIndex(e => e.SourceDocumentId);
            entity.Property(e => e.Title).HasMaxLength(300);
            entity.Property(e => e.MedicationName).HasMaxLength(200);
            entity.Property(e => e.Dosage).HasMaxLength(100);
            entity.Property(e => e.Frequency).HasMaxLength(100);
            entity.Property(e => e.Duration).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.Summary).HasMaxLength(2000);
        });

        // ChatMessage configuration
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}