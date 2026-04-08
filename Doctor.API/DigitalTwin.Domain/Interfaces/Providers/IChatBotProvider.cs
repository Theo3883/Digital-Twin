using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Providers;

public interface IChatBotProvider
{
    Task<string> SendMessageAsync(string userMessage, PatientProfile? context, CancellationToken ct = default);
}
