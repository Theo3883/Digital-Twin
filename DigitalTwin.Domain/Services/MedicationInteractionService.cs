using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Services;

public class MedicationInteractionService
{
    public InteractionSeverity EvaluateSeverity(MedicationInteraction interaction)
    {
        return interaction.Severity;
    }

    public bool HasHighRisk(IEnumerable<MedicationInteraction> interactions)
    {
        return interactions.Any(i => i.Severity == InteractionSeverity.High);
    }

    public IEnumerable<MedicationInteraction> FilterByMinSeverity(
        IEnumerable<MedicationInteraction> interactions,
        InteractionSeverity minSeverity)
    {
        return interactions.Where(i => i.Severity >= minSeverity);
    }
}
