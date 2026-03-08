using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Options;

namespace DigitalTwin.Integrations.AI;

/// <summary>
/// Implements <see cref="IChatBotProvider"/> using Gemini Pro.
/// Builds chat prompt from template + patient context, delegates HTTP to <see cref="IGeminiApiClient"/>.
/// </summary>
public class GeminiChatBotProvider : IChatBotProvider
{
    private readonly IGeminiApiClient _apiClient;
    private readonly GeminiPromptOptions _options;

    public GeminiChatBotProvider(
        IGeminiApiClient apiClient,
        IOptions<GeminiPromptOptions> options)
    {
        _apiClient = apiClient;
        _options   = options.Value;
    }

    public async Task<string> SendMessageAsync(string userMessage, PatientProfile? context, CancellationToken ct = default)
    {
        var systemPrompt = BuildSystemPrompt(context);
        return await _apiClient.GenerateContentAsync(systemPrompt, userMessage, ct);
    }

    private string BuildSystemPrompt(PatientProfile? profile)
    {
        var identity = _options.SystemIdentityPrompt;
        var format   = _options.ChatResponseFormatPrompt;

        if (profile is null)
        {
            // Remove patient context placeholders when no profile is available.
            format = format
                .Replace("{patientName}", "Unknown")
                .Replace("{age}", "Unknown")
                .Replace("{medications}", "None available")
                .Replace("{latestHr}", "N/A")
                .Replace("{latestSpO2}", "N/A")
                .Replace("{recentSteps}", "N/A")
                .Replace("{trend}", "N/A");
        }
        else
        {
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
                .Replace("{age}", CalculateAge(profile.DateOfBirth))
                .Replace("{medications}", medications)
                .Replace("{latestHr}", latestHr?.Value.ToString("F0") ?? "N/A")
                .Replace("{latestSpO2}", latestSpO2?.Value.ToString("F0") ?? "N/A")
                .Replace("{recentSteps}", "N/A")
                .Replace("{trend}", "Stable");
        }

        return $"{identity}\n\n{format}";
    }

    private static string CalculateAge(DateTime dateOfBirth)
    {
        if (dateOfBirth == default) return "Unknown";
        var age = DateTime.Today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > DateTime.Today.AddYears(-age)) age--;
        return age.ToString();
    }
}
