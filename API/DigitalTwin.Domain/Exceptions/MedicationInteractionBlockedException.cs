using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Exceptions;

/// <summary>
/// Thrown when a medication cannot be added because it would create a high-risk
/// drug interaction with one or more of the patient's existing active medications.
/// </summary>
public sealed class MedicationInteractionBlockedException : DomainException
{
    public IReadOnlyList<MedicationInteraction> BlockingInteractions { get; }

    public MedicationInteractionBlockedException(IReadOnlyList<MedicationInteraction> blockingInteractions)
        : base(BuildMessage(blockingInteractions))
    {
        BlockingInteractions = blockingInteractions;
    }

    private static string BuildMessage(IReadOnlyList<MedicationInteraction> interactions)
    {
        var descriptions = interactions
            .Select(i => i.Description)
            .Where(d => !string.IsNullOrWhiteSpace(d));

        return $"Medication cannot be added due to {interactions.Count} high-risk interaction(s): "
               + string.Join("; ", descriptions);
    }
}
