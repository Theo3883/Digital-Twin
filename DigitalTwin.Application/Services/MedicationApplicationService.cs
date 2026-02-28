using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Application.Mappers;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;

namespace DigitalTwin.Application.Services;

public class MedicationApplicationService : IMedicationApplicationService
{
    private readonly IMedicationInteractionProvider _provider;
    private readonly IMedicationInteractionService _interactionService;

    public MedicationApplicationService(
        IMedicationInteractionProvider provider,
        IMedicationInteractionService interactionService)
    {
        _provider = provider;
        _interactionService = interactionService;
    }

    public async Task<IEnumerable<MedicationInteractionDto>> CheckInteractionsAsync(
        IEnumerable<string> rxCuis)
    {
        var interactions = await _provider.GetInteractionsAsync(rxCuis);
        return interactions.Select(MedicationInteractionMapper.ToDto);
    }

    public async Task<bool> HasHighRiskInteractionsAsync(IEnumerable<string> rxCuis)
    {
        var interactions = await _provider.GetInteractionsAsync(rxCuis);
        return _interactionService.HasHighRisk(interactions);
    }
}
