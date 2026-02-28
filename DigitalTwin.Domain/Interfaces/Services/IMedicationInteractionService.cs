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
}
