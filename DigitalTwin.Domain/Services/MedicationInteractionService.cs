using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Services;

public class MedicationInteractionService : IMedicationInteractionService
{
    /// <summary>
    /// The minimum severity that constitutes a blocking interaction.
    /// Centralised here so the threshold is changed in exactly one place.
    /// </summary>
    private const InteractionSeverity BlockingThreshold = InteractionSeverity.High;

    public InteractionSeverity EvaluateSeverity(MedicationInteraction interaction)
        => interaction.Severity;

    public bool HasHighRisk(IEnumerable<MedicationInteraction> interactions)
        => interactions.Any(i => i.Severity == InteractionSeverity.High);

    public IEnumerable<MedicationInteraction> FilterByMinSeverity(
        IEnumerable<MedicationInteraction> interactions,
        InteractionSeverity minSeverity)
        => interactions.Where(i => i.Severity >= minSeverity);

    /// <inheritdoc/>
    public bool IsAdditionBlocked(IEnumerable<MedicationInteraction> interactions)
        => GetBlockingInteractions(interactions).Any();

    /// <inheritdoc/>
    public IEnumerable<MedicationInteraction> GetBlockingInteractions(
        IEnumerable<MedicationInteraction> interactions)
        => interactions.Where(i => i.Severity >= BlockingThreshold);
}
