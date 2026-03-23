using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Services;

/// <summary>
/// Domain service owning all medication business rules.
/// The application layer is a pure orchestrator that calls this service
/// for construction/validation and then delegates persistence to the repository.
/// </summary>
public interface IMedicationService
{
    /// <summary>
    /// Creates a valid <see cref="Medication"/> domain object, applying defaults and validating inputs.
    /// Does NOT persist — caller must call the repository.
    /// </summary>
    Medication CreateMedication(CreateMedicationRequest request);

    /// <summary>
    /// Validates that <paramref name="medication"/> belongs to <paramref name="patientId"/>.
    /// Throws <see cref="Exceptions.MedicationOwnershipException"/> when ownership check fails.
    /// </summary>
    void ValidateOwnership(Guid patientId, Medication medication);

    /// <summary>
    /// Returns a new <see cref="Medication"/> with status set to Discontinued and EndDate/DiscontinuedReason populated.
    /// Pure function — does NOT persist.
    /// </summary>
    Medication Discontinue(Medication medication, string? reason);
}
