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
}
