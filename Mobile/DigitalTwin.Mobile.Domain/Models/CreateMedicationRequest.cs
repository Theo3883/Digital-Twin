using DigitalTwin.Mobile.Domain.Enums;

namespace DigitalTwin.Mobile.Domain.Models;

public record CreateMedicationRequest(
    Guid PatientId,
    string Name,
    string Dosage,
    string? Frequency,
    MedicationRoute Route,
    string? RxCui,
    string? Instructions,
    string? Reason,
    Guid? PrescribedByUserId,
    DateTime? StartDate,
    AddedByRole AddedByRole);
