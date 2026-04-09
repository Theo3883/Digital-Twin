namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface IChatBotProvider
{
    Task<string> SendMessageAsync(string userMessage, string? patientContext, CancellationToken ct = default);
}
