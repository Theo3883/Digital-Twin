using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

public interface IChatBotApplicationService
{
    Task<ChatMessageDto> SendMessageAsync(string userMessage, CancellationToken ct = default);
}
