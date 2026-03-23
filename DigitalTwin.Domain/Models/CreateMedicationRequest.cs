using DigitalTwin.Domain.Enums;

namespace DigitalTwin.Domain.Models;

/// <summary>
/// Parameter object for <see cref="Interfaces.Services.IMedicationService.CreateMedication"/>.
/// Reduces the method to a single parameter, keeping the API clean and extendable.
/// </summary>
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
