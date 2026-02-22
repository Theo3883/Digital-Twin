namespace DigitalTwin.Infrastructure.Entities;

public class PatientEntity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string? BloodType { get; set; }
    public string? Allergies { get; set; }
    public string? MedicalHistoryNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDirty { get; set; } = true;
    public DateTime? SyncedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public UserEntity User { get; set; } = null!;
    public ICollection<VitalSignEntity> VitalSigns { get; set; } = [];
    public ICollection<MedicationEntity> Medications { get; set; } = [];
    public ICollection<SleepSessionEntity> SleepSessions { get; set; } = [];
}
