namespace DigitalTwin.Domain.Models;

public class Patient
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string? BloodType { get; set; }
    public string? Allergies { get; set; }
    public string? MedicalHistoryNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
