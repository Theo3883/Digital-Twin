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
        CreatedAt           = m.CreatedAt,
        UpdatedAt           = m.UpdatedAt
    };
}
