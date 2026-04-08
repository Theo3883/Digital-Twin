using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Services;

/// <summary>
/// In-memory singleton that holds the MedicalAssistant chat history for the lifetime of the app process.
/// Messages are automatically gone when the app restarts.
/// </summary>
public sealed class ChatHistoryStore
{
    private readonly List<ChatMessageDto> _messages = [];

    public IReadOnlyList<ChatMessageDto> Messages => _messages;

    public void Add(ChatMessageDto message) => _messages.Add(message);

    public void Clear() => _messages.Clear();
}
