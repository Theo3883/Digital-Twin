namespace DigitalTwin.Infrastructure.Entities;

public class PatientEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string? BloodType { get; set; }
    public string? Allergies { get; set; }
    public string? MedicalHistoryNotes { get; set; }

    /// <summary>Patient weight in kilograms (kg).</summary>
    public decimal? Weight { get; set; }

    /// <summary>Patient height in centimeters (cm).</summary>
    public decimal? Height { get; set; }

    /// <summary>Blood pressure systolic value in millimeters of mercury (mmHg).</summary>
    public int? BloodPressureSystolic { get; set; }

    /// <summary>Blood pressure diastolic value in millimeters of mercury (mmHg).</summary>
    public int? BloodPressureDiastolic { get; set; }

    /// <summary>Total cholesterol in millimoles per liter (mmol/L).</summary>
    public decimal? Cholesterol { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDirty { get; set; }
    public DateTime? SyncedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public UserEntity User { get; set; } = null!;
    public ICollection<VitalSignEntity> VitalSigns { get; set; } = [];
    public ICollection<MedicationEntity> Medications { get; set; } = [];
    public ICollection<SleepSessionEntity> SleepSessions { get; set; } = [];
}
