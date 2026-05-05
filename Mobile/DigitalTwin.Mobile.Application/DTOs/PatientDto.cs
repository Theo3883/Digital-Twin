namespace DigitalTwin.Mobile.Application.DTOs;

public record PatientDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string? BloodType { get; init; }
    public string? Allergies { get; init; }
    public string? MedicalHistoryNotes { get; init; }
    public double? Weight { get; init; }
    public double? Height { get; init; }
    public int? BloodPressureSystolic { get; init; }
    public int? BloodPressureDiastolic { get; init; }
    public double? Cholesterol { get; init; }
    public string? Cnp { get; init; }
    public bool IsSynced { get; init; }
}

public record PatientUpdateInput
{
    public string? BloodType { get; init; }
    public string? Allergies { get; init; }
    public string? MedicalHistoryNotes { get; init; }
    public double? Weight { get; init; }
    public double? Height { get; init; }
    public int? BloodPressureSystolic { get; init; }
    public int? BloodPressureDiastolic { get; init; }
    public double? Cholesterol { get; init; }
    public string? Cnp { get; init; }
}