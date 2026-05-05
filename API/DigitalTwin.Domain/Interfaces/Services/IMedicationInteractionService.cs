using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface IMedicationInteractionService
{
    InteractionSeverity EvaluateSeverity(MedicationInteraction interaction);
    bool HasHighRisk(IEnumerable<MedicationInteraction> interactions);
    IEnumerable<MedicationInteraction> FilterByMinSeverity(
        IEnumerable<MedicationInteraction> interactions,
        InteractionSeverity minSeverity);

    /// <summary>
    /// Business rule: determines whether a set of interactions should block
    /// a medication from being added. Currently blocks on any High-severity hit.
    /// </summary>
    bool IsAdditionBlocked(IEnumerable<MedicationInteraction> interactions);

    /// <summary>
    /// Returns only the interactions whose severity meets the blocking threshold.
    /// </summary>
    IEnumerable<MedicationInteraction> GetBlockingInteractions(
        IEnumerable<MedicationInteraction> interactions);
}
