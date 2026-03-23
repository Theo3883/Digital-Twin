using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Options;

namespace DigitalTwin.Integrations.AI;

/// <summary>
/// Implements <see cref="ICoachingProvider"/> using Gemini Pro.
/// builds coaching prompt from template + patient profile, delegates HTTP to <see cref="IGeminiApiClient"/>.
/// </summary>
public class GeminiCoachingProvider : ICoachingProvider
{
    private readonly IGeminiApiClient _apiClient;
    private readonly GeminiPromptOptions _options;

    public GeminiCoachingProvider(
        IGeminiApiClient apiClient,
        IOptions<GeminiPromptOptions> options)
    {
        _apiClient = apiClient;
        _options   = options.Value;
    }

    public async Task<string> GetAdviceAsync(PatientProfile profile)
    {
        var systemPrompt = BuildSystemPrompt(profile);
        var userMessage  = "Generate my personalized health coaching advice based on my current data.";

        return await _apiClient.GenerateContentAsync(systemPrompt, userMessage);
    }

    private string BuildSystemPrompt(PatientProfile profile)
    {
        var identity = _options.SystemIdentityPrompt;
        var format   = _options.CoachingResponseFormatPrompt;

        var latestHr = profile.RecentVitals
            .Where(v => v.Type == Domain.Enums.VitalSignType.HeartRate)
            .MaxBy(v => v.Timestamp);

        var latestSpO2 = profile.RecentVitals
            .Where(v => v.Type == Domain.Enums.VitalSignType.SpO2)
            .MaxBy(v => v.Timestamp);

        var medications = profile.CurrentMedications.Count > 0
            ? string.Join(", ", profile.CurrentMedications.Select(m => m.Name))
            : "None reported";

        format = format
            .Replace("{patientName}", profile.FullName)
            .Replace("{latestHr}", latestHr?.Value.ToString("F0") ?? "N/A")
            .Replace("{hrTrend}", "Stable")
            .Replace("{latestSpO2}", latestSpO2?.Value.ToString("F0") ?? "N/A")
            .Replace("{stepsToday}", "N/A")
            .Replace("{medications}", medications)
            .Replace("{sleepScore}", "N/A");

        return $"{identity}\n\n{format}";
    }
}
