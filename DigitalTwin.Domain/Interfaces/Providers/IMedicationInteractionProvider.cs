using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Providers;

public interface IMedicationInteractionProvider
{
    Task<IEnumerable<MedicationInteraction>> GetInteractionsAsync(IEnumerable<string> rxCuis);
}
