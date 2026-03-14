using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces.Services;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Services;

public class MedicationService : IMedicationService
{
    public Medication CreateMedication(CreateMedicationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Medication name is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Dosage))
            throw new ArgumentException("Dosage is required.", nameof(request));

        var now = DateTime.UtcNow;

        return new Medication
        {
            Id = Guid.NewGuid(),
            PatientId = request.PatientId,
            Name = request.Name.Trim(),
            Dosage = request.Dosage.Trim(),
            Frequency = request.Frequency?.Trim(),
            Route = request.Route,
            RxCui = request.RxCui?.Trim(),
            Instructions = request.Instructions?.Trim(),
            Reason = request.Reason?.Trim(),
            PrescribedByUserId = request.PrescribedByUserId,
            StartDate = request.StartDate,
            Status = MedicationStatus.Active,
            AddedByRole = request.AddedByRole,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
