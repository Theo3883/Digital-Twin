using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface IMedicationInteractionProvider
{
    Task<IEnumerable<MedicationInteraction>> GetInteractionsAsync(IEnumerable<string> rxCuis, CancellationToken ct = default);
}
