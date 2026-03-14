using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces.Services;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Services;

public class MedicationService : IMedicationService
{
    public Medication CreateMedication(
        Guid patientId,
        string name,
        string dosage,
        string? frequency,
        MedicationRoute route,
        string? rxCui,
        string? instructions,
        string? reason,
        Guid? prescribedByUserId,
        DateTime? startDate,
        AddedByRole addedByRole)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Medication name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(dosage))
            throw new ArgumentException("Dosage is required.", nameof(dosage));

        var now = DateTime.UtcNow;

        return new Medication
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            Name = name.Trim(),
            Dosage = dosage.Trim(),
            Frequency = frequency?.Trim(),
            Route = route,
            RxCui = rxCui?.Trim(),
            Instructions = instructions?.Trim(),
            Reason = reason?.Trim(),
            PrescribedByUserId = prescribedByUserId,
            StartDate = startDate,
            Status = MedicationStatus.Active,
            AddedByRole = addedByRole,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
