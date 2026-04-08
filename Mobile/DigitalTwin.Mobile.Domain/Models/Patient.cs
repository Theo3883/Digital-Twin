namespace DigitalTwin.Mobile.Domain.Models;

/// <summary>
/// Mobile patient profile - simplified for mobile app needs
/// </summary>
public class Patient
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? BloodType { get; set; }
    public string? Allergies { get; set; }
    public string? MedicalHistoryNotes { get; set; }
    public double? Weight { get; set; }
    public double? Height { get; set; }
    public int? BloodPressureSystolic { get; set; }
    public int? BloodPressureDiastolic { get; set; }
    public double? Cholesterol { get; set; }
    public string? Cnp { get; set; }
    
    // Mobile-specific properties
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsSynced { get; set; } = false;
}