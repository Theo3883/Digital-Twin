using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Entities;

namespace DigitalTwin.Infrastructure.Mappers;

internal static class PatientEntityMapper
{
    internal static Patient ToDomain(PatientEntity e) => new()
    {
        Id                  = e.Id,
        UserId              = e.UserId,
        BloodType           = e.BloodType,
        Allergies           = e.Allergies,
        MedicalHistoryNotes = e.MedicalHistoryNotes,
        Weight              = e.Weight,
        Height              = e.Height,
        BloodPressureSystolic  = e.BloodPressureSystolic,
        BloodPressureDiastolic = e.BloodPressureDiastolic,
        Cholesterol         = e.Cholesterol,
        CreatedAt           = e.CreatedAt,
        UpdatedAt           = e.UpdatedAt
    };

    internal static PatientEntity ToEntity(Patient m) => new()
    {
        Id                  = m.Id,
        UserId              = m.UserId,
        BloodType           = m.BloodType,
        Allergies           = m.Allergies,
        MedicalHistoryNotes = m.MedicalHistoryNotes,
        Weight              = m.Weight,
        Height              = m.Height,
        BloodPressureSystolic  = m.BloodPressureSystolic,
        BloodPressureDiastolic = m.BloodPressureDiastolic,
        Cholesterol         = m.Cholesterol,
        CreatedAt           = m.CreatedAt,
        UpdatedAt           = m.UpdatedAt
    };
}
