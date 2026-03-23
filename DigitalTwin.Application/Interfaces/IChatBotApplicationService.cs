using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Defines the application contract for chatbot interactions.
/// </summary>
public interface IChatBotApplicationService
{
    /// <summary>
    /// Sends a user message to the chatbot and returns the assistant response.
    /// </summary>
    Task<ChatMessageDto> SendMessageAsync(string userMessage, CancellationToken ct = default);
}
