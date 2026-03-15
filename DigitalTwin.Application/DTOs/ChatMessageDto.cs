namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents a chat message exchanged with the chatbot UI.
/// </summary>
public class ChatMessageDto
{
    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the message was authored by the user.
    /// </summary>
    public bool IsUser { get; set; }

    /// <summary>
    /// Gets or sets when the message was created.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
