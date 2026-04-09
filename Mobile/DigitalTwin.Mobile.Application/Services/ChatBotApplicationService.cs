using DigitalTwin.Mobile.Application.DTOs;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

public class ChatBotApplicationService
{
    private readonly IChatBotProvider _chatProvider;
    private readonly IChatMessageRepository _chatRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly ILogger<ChatBotApplicationService> _logger;

    public ChatBotApplicationService(
        IChatBotProvider chatProvider,
        IChatMessageRepository chatRepo,
        IPatientRepository patientRepo,
        ILogger<ChatBotApplicationService> logger)
    {
        _chatProvider = chatProvider;
        _chatRepo = chatRepo;
        _patientRepo = patientRepo;
        _logger = logger;
    }

    public async Task<ChatMessageDto> SendMessageAsync(string userMessage)
    {
        // Save user message
        var userMsg = new ChatMessage
        {
            Content = userMessage,
            IsUser = true
        };
        await _chatRepo.SaveAsync(userMsg);

        try
        {
            // Build patient context
            var patient = await _patientRepo.GetCurrentPatientAsync();
            string? context = null;
            if (patient != null)
            {
                context = $"Patient: BloodType={patient.BloodType}, Weight={patient.Weight}kg, Height={patient.Height}cm, Allergies={patient.Allergies}";
            }

            var response = await _chatProvider.SendMessageAsync(userMessage, context);

            // Save AI response
            var aiMsg = new ChatMessage
            {
                Content = response,
                IsUser = false
            };
            await _chatRepo.SaveAsync(aiMsg);

            _logger.LogInformation("[ChatBot] Sent message, got response ({Length} chars)", response.Length);

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
            _logger.LogError(ex, "[ChatBot] Failed to get AI response");

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
}
