using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Interfaces.Services;
using DigitalTwin.Domain.Models;
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

    // Cached per chat session (scoped lifetime). Built once on the first message with a
    // generous timeout so a slow Keychain/DB call never delays subsequent messages.
    private PatientProfile? _cachedProfile;
    private bool _profileLoaded;

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
        if (!_profileLoaded)
        {
            // Attempt to build patient context within 5 seconds. If Keychain or DB is
            // slow/locked the AI still responds — just without personalised context.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                _cachedProfile = await _patientContextService.BuildContextAsync(cts.Token);
            }
            catch (Exception ex)
            {
                _cachedProfile = null;
                _logger.LogWarning(ex, "[ChatBot] Patient context unavailable; proceeding without personalisation.");
            }
            _profileLoaded = true;
        }

        _logger.LogInformation("[ChatBot] Sending message to provider.");

        var response = await _chatBotProvider.SendMessageAsync(userMessage, _cachedProfile, ct);

        return new ChatMessageDto
        {
            Content   = response,
            IsUser    = false,
            Timestamp = DateTime.UtcNow
        };
    }
}
