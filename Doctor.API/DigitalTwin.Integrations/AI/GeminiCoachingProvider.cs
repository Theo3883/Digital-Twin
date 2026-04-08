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
        format = GeminiPatientContextFormatting.ReplaceMedicalProfilePlaceholders(format, profile);

        return $"{identity}\n\n{format}";
    }

    public async Task<string> GetEnvironmentAdviceAsync(PatientProfile? profile, EnvironmentReading environment, CancellationToken cancellationToken = default)
    {
        var latestHr = profile?.RecentVitals
            .Where(v => v.Type == Domain.Enums.VitalSignType.HeartRate)
            .MaxBy(v => v.Timestamp);

        var name = profile?.FullName ?? "there";
        var loc = string.IsNullOrWhiteSpace(environment.LocationDisplayName) ? "your area" : environment.LocationDisplayName;
        var hrText = latestHr is not null ? $"{latestHr.Value:F0}" : "unknown";

        var system = _options.SystemIdentityPrompt + "\n\n" + _options.EnvironmentAdvicePrompt
            .Replace("{location}", loc)
            .Replace("{aqi}", environment.AqiIndex > 0 ? environment.AqiIndex.ToString() : "n/a")
            .Replace("{pm25}", environment.PM25.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{tempC}", environment.Temperature.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{latestHr}", hrText)
            .Replace("{patientName}", name);

        return await _apiClient.GenerateContentAsync(system, "Respond now.", cancellationToken).ConfigureAwait(false);
    }
}
