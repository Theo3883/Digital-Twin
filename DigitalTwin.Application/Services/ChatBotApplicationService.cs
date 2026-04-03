using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Interfaces.Services;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Application.Services;

/// <summary>
/// Orchestrates chatbot requests by loading patient context and delegating to the chat provider.
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

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatBotApplicationService"/> class.
    /// </summary>
    public ChatBotApplicationService(
        IChatBotProvider chatBotProvider,
        IPatientContextService patientContextService,
        ILogger<ChatBotApplicationService> logger)
    {
        _chatBotProvider       = chatBotProvider;
        _patientContextService = patientContextService;
        _logger                = logger;
    }

    /// <summary>
    /// Sends a message to the chatbot provider and returns the generated reply.
    /// </summary>
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

        var providerType = _chatBotProvider.GetType().Name;
        _logger.LogInformation("[ChatBot] Sending message to provider ({Provider}).", providerType);
        if (providerType.Contains("Mock", StringComparison.OrdinalIgnoreCase))
            _logger.LogWarning("[ChatBot] Gemini is NOT active — GEMINI_API_KEY is not set. " +
                "Add GEMINI_API_KEY=<your-key> to your .env file at the project root to enable real AI responses.");

        var response = await _chatBotProvider.SendMessageAsync(userMessage, _cachedProfile, ct);

        return new ChatMessageDto
        {
            Content   = response,
            IsUser    = false,
            Timestamp = DateTime.UtcNow
        };
    }
}
