namespace DigitalTwin.Domain.Models;

public sealed record PatientProfileUpdate(
    string? BloodType,
    string? Allergies,
    string? MedicalHistoryNotes,
    decimal? Weight,
    decimal? Height,
    int? BloodPressureSystolic,
    int? BloodPressureDiastolic,
    decimal? Cholesterol,
    string? Cnp,
    DateTime? UserDateOfBirth);
