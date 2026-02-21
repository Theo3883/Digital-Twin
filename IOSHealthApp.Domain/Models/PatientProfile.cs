namespace IOSHealthApp.Domain.Models;

public class PatientProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FullName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public List<Medication> CurrentMedications { get; set; } = [];
    public List<VitalSign> RecentVitals { get; set; } = [];
}
