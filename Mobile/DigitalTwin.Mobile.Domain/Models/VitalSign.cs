using DigitalTwin.Mobile.Domain.Enums;

namespace DigitalTwin.Mobile.Domain.Models;

/// <summary>
/// Mobile vital sign reading - simplified for mobile app needs
/// </summary>
public class VitalSign
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public VitalSignType Type { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Source { get; set; } = "Mobile";
    public DateTime Timestamp { get; set; }
    
    // Mobile-specific properties
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsSynced { get; set; } = false;
}