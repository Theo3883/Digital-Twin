using System.Diagnostics;
using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

public class ChatBotApplicationService
{
    private readonly IChatBotProvider _chatProvider;
    private readonly IChatMessageRepository _chatRepo;
    private readonly PatientAiContextBuilder _contextBuilder;
    private readonly ILogger<ChatBotApplicationService> _logger;

    public ChatBotApplicationService(
        IChatBotProvider chatProvider,
        IChatMessageRepository chatRepo,
        PatientAiContextBuilder contextBuilder,
        ILogger<ChatBotApplicationService> logger)
    {
        _chatProvider = chatProvider;
        _chatRepo = chatRepo;
        _contextBuilder = contextBuilder;
        _logger = logger;
    }

    public async Task<ChatMessageDto> SendMessageAsync(string userMessage)
    {
        var correlationId = ResolveCorrelationId();

        _logger.LogInformation(
            "[ChatBot][{CorrelationId}] Sending chat message ({Length} chars).",
            correlationId,
            userMessage.Length);

        // Save user message
        var userMsg = new ChatMessage
        {
            Content = userMessage,
            IsUser = true
        };
        await _chatRepo.SaveAsync(userMsg);

        try
        {
            var context = await _contextBuilder.BuildChatContextAsync();

            var response = await _chatProvider.SendMessageAsync(userMessage, context);

            // Save AI response
            var aiMsg = new ChatMessage
            {
                Content = response,
                IsUser = false
            };
            await _chatRepo.SaveAsync(aiMsg);

            if (IsDegradedGeminiResponse(response))
            {
                _logger.LogWarning(
                    "[ChatBot][{CorrelationId}] Provider returned degraded response ({Length} chars).",
                    correlationId,
                    response.Length);
            }
            else
            {
                _logger.LogInformation(
                    "[ChatBot][{CorrelationId}] Sent message, got response ({Length} chars).",
                    correlationId,
                    response.Length);
            }

            return new ChatMessageDto
            {
                Id = aiMsg.Id,
                Content = aiMsg.Content,
                IsUser = false,
                Timestamp = aiMsg.Timestamp
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ChatBot][{CorrelationId}] Failed to get AI response", correlationId);

            var errorMsg = new ChatMessage
            {
                Content = "I'm sorry, I'm unable to respond right now. Please try again later.",
                IsUser = false
            };
            await _chatRepo.SaveAsync(errorMsg);

            return new ChatMessageDto
            {
                Id = errorMsg.Id,
                Content = errorMsg.Content,
                IsUser = false,
                Timestamp = errorMsg.Timestamp
            };
        }
    }

    public async Task<IEnumerable<ChatMessageDto>> GetChatHistoryAsync()
    {
        var messages = await _chatRepo.GetAllAsync();
        return messages.Select(m => new ChatMessageDto
        {
            Id = m.Id,
            Content = m.Content,
            IsUser = m.IsUser,
            Timestamp = m.Timestamp
        });
    }

    public async Task ClearChatHistoryAsync()
    {
        await _chatRepo.ClearAllAsync();
        _logger.LogInformation("[ChatBot] Chat history cleared");
    }

    private static bool IsDegradedGeminiResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return true;

        return response.Contains("temporarily rate limited", StringComparison.OrdinalIgnoreCase) ||
               response.Contains("trouble connecting", StringComparison.OrdinalIgnoreCase) ||
               response.Contains("error occurred", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveCorrelationId()
    {
        var traceId = Activity.Current?.TraceId.ToString();
        return string.IsNullOrWhiteSpace(traceId)
            ? Guid.NewGuid().ToString("N")[..8]
            : traceId;
    }
}
