using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Thin orchestrator: delegates context building to the Domain service,
/// delegates AI call to <see cref="IChatBotProvider"/>, maps to DTO.
/// </summary>
public class ChatBotApplicationService : IChatBotApplicationService
{
    private readonly IChatBotProvider _chatBotProvider;
    private readonly IPatientContextService _patientContextService;
    private readonly ILogger<ChatBotApplicationService> _logger;

    public ChatBotApplicationService(
        IChatBotProvider chatBotProvider,
        IPatientContextService patientContextService,
        ILogger<ChatBotApplicationService> logger)
    {
        _chatBotProvider       = chatBotProvider;
        _patientContextService = patientContextService;
        _logger                = logger;
    }

    public async Task<ChatMessageDto> SendMessageAsync(string userMessage, CancellationToken ct = default)
    {
        var profile = await _patientContextService.BuildContextAsync(ct);

        _logger.LogInformation("[ChatBot] Sending message to provider.");

        var response = await _chatBotProvider.SendMessageAsync(userMessage, profile, ct);

        return new ChatMessageDto
        {
            Content   = response,
            IsUser    = false,
            Timestamp = DateTime.UtcNow
        };
    }
}
