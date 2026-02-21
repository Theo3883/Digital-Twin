using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces;

public interface IMedicationInteractionProvider
{
    Task<IEnumerable<MedicationInteraction>> GetInteractionsAsync(IEnumerable<string> rxCuis);
}
