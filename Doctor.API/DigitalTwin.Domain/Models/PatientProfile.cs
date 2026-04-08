using DigitalTwin.Domain.Enums;

namespace DigitalTwin.Domain.Models;

public class PatientProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FullName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public List<Medication> CurrentMedications { get; set; } = [];
    public List<VitalSign> RecentVitals { get; set; } = [];

    /// <summary>Per-type trend: +1 rising, 0 stable, -1 falling.</summary>
    public Dictionary<VitalSignType, int> VitalTrends { get; set; } = [];

    /// <summary>Blood type label (e.g. A+), if recorded.</summary>
    public string? BloodType { get; set; }

    /// <summary>Allergy notes, if recorded.</summary>
    public string? Allergies { get; set; }

    /// <summary>Free-text medical history notes, if recorded.</summary>
    public string? MedicalHistoryNotes { get; set; }

    /// <summary>Weight in kilograms (kg).</summary>
    public decimal? Weight { get; set; }

    /// <summary>Height in centimeters (cm).</summary>
    public decimal? Height { get; set; }

    /// <summary>Blood pressure systolic in millimeters of mercury (mmHg).</summary>
    public int? BloodPressureSystolic { get; set; }

    /// <summary>Blood pressure diastolic in millimeters of mercury (mmHg).</summary>
    public int? BloodPressureDiastolic { get; set; }

    /// <summary>Total cholesterol in millimoles per liter (mmol/L).</summary>
    public decimal? Cholesterol { get; set; }

    /// <summary>Body mass index (kg/m²), derived from weight and height when both are present.</summary>
    public decimal? Bmi { get; set; }

    /// <summary>Estimated resting heart rate in beats per minute (BPM), derived from recent heart-rate samples.</summary>
    public int? RestingHeartRateBpm { get; set; }
}
