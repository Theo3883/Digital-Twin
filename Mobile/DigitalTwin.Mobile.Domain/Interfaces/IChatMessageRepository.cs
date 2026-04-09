using DigitalTwin.Mobile.Domain.Models;

namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface IChatMessageRepository
{
    Task<IEnumerable<ChatMessage>> GetAllAsync();
    Task SaveAsync(ChatMessage message);
    Task ClearAllAsync();
}
