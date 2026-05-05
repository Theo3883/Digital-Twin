using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Services;

public class MedicationInteractionService
{
    private const InteractionSeverity BlockingThreshold = InteractionSeverity.High;

    public InteractionSeverity EvaluateSeverity(MedicationInteraction interaction)
        => interaction.Severity;

    public bool HasHighRisk(IEnumerable<MedicationInteraction> interactions)
        => interactions.Any(i => i.Severity == InteractionSeverity.High);

    public IEnumerable<MedicationInteraction> FilterByMinSeverity(
        IEnumerable<MedicationInteraction> interactions,
        InteractionSeverity minSeverity)
        => interactions.Where(i => i.Severity >= minSeverity);

    public bool IsAdditionBlocked(IEnumerable<MedicationInteraction> interactions)
        => GetBlockingInteractions(interactions).Any();

    public IEnumerable<MedicationInteraction> GetBlockingInteractions(
        IEnumerable<MedicationInteraction> interactions)
        => interactions.Where(i => i.Severity >= BlockingThreshold);
}
