using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Integrations.Mocks;

/// <summary>
/// Fallback <see cref="IChatBotProvider"/> used when no Gemini API key is configured.
/// Returns canned responses to keep the app functional without an external API.
/// LSP: fully substitutable for the real provider.
/// </summary>
public class MockChatBotProvider : IChatBotProvider
{
    private static readonly Dictionary<string, string> TopicResponses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["heart"] = "**Your heart health is our top priority.**\n\nBased on standard guidelines, a healthy resting heart rate is between 60-100 bpm. Regular monitoring helps detect changes early.\n\n- Heart: Keep tracking your resting heart rate daily\n- Exercise: Moderate aerobic activity supports heart health\n\n*Stay consistent with your monitoring routine!*",

        ["sleep"] = "**Good sleep is essential for heart health.**\n\nAdults need 7-9 hours of quality sleep per night. Poor sleep quality has been linked to higher cardiovascular risk.\n\n- Sleep: Maintain a consistent bedtime schedule\n- Heart: Quality sleep helps regulate blood pressure\n\n*A good night's rest is one of the best things you can do for your heart.*",

        ["medication"] = "**Medication adherence is crucial for your treatment plan.**\n\nAlways take medications as prescribed by your doctor. Never adjust dosages without consulting your healthcare provider.\n\n- Medication: Take your medications at the same time each day\n- Heart: Report any side effects to your doctor promptly\n\n*Your doctor is the best person to guide medication decisions.*",

        ["exercise"] = "**Regular exercise supports cardiovascular health.**\n\nThe American Heart Association recommends at least 150 minutes of moderate aerobic activity per week.\n\n- Exercise: Start with short walks and gradually increase duration\n- Heart: Monitor your heart rate during activity\n\n*Every step counts towards a healthier heart!*"
    };

    private const string DefaultResponse = "**I'm here to help with your cardiac health questions.**\n\nYou can ask me about your heart rate, medications, sleep patterns, exercise routines, or any other heart health topics.\n\n- Heart: I can help interpret your vital sign trends\n- Exercise: I can suggest activity recommendations\n- Sleep: I can discuss sleep and heart health connections\n\n*Feel free to ask anything about your heart health!*";

    public Task<string> SendMessageAsync(string userMessage, PatientProfile? context, CancellationToken ct = default)
    {
        var response = TopicResponses
            .FirstOrDefault(kvp => userMessage.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            .Value ?? DefaultResponse;

        return Task.FromResult(response);
    }
}
