using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Services;

public class MedicationService
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
            UpdatedAt = now,
            IsSynced = false
        };
    }

    public void ValidateOwnership(Guid patientId, Medication medication)
    {
        if (medication.PatientId != patientId)
            throw new InvalidOperationException($"Medication {medication.Id} does not belong to patient {patientId}.");
    }

    public Medication Discontinue(Medication medication, string? reason)
    {
        var now = DateTime.UtcNow;
        medication.Status = MedicationStatus.Discontinued;
        medication.EndDate = now;
        medication.DiscontinuedReason = reason?.Trim();
        medication.UpdatedAt = now;
        medication.IsSynced = false;
        return medication;
    }
}
