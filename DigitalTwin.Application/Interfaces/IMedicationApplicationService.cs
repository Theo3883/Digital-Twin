using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

public interface IMedicationApplicationService
{
    Task<IEnumerable<MedicationInteractionDto>> CheckInteractionsAsync(IEnumerable<string> rxCuis);
    Task<bool> HasHighRiskInteractionsAsync(IEnumerable<string> rxCuis);
}
