using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalTwin.Mobile.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Infrastructure.Services;

public class GeminiChatService : IChatBotProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiChatService> _logger;
    private readonly string _apiKey;

    public GeminiChatService(HttpClient httpClient, string apiKey, ILogger<GeminiChatService> logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _logger = logger;
    }

    public async Task<string> SendMessageAsync(string userMessage, string? patientContext, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return "AI assistant is not configured. Please set up your Gemini API key.";

        try
        {
            var systemPrompt = "You are a helpful medical assistant for a digital twin health app. " +
                "Provide general health guidance. Never diagnose conditions.";
            if (!string.IsNullOrEmpty(patientContext))
                systemPrompt += $"\n\nPatient context: {patientContext}";

            var request = new GeminiRequest
            {
                SystemInstruction = new GeminiContent
                {
                    Parts = [new GeminiPart { Text = systemPrompt }]
                },
                Contents =
                [
                    new GeminiContentEntry
                    {
                        Role = "user",
                        Parts = [new GeminiPart { Text = userMessage }]
                    }
                ],
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = 0.7f,
                    TopP = 0.9f,
                    TopK = 40,
                    MaxOutputTokens = 2048
                }
            };

            var url = $"?key={_apiKey}";
            using var content = new StringContent(
                JsonSerializer.Serialize(request, IntegrationJsonContext.Default.GeminiRequest),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[Gemini] Chat request failed: {Status}", response.StatusCode);
                return "I'm having trouble connecting to the AI service right now.";
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var geminiResponse = await JsonSerializer.DeserializeAsync(stream, IntegrationJsonContext.Default.GeminiResponse, ct);

            var text = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            return text ?? "I wasn't able to generate a response. Please try again.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Gemini] Chat request exception");
            return "An error occurred while communicating with the AI assistant.";
        }
    }
}

public class GeminiCoachingService : ICoachingProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiCoachingService> _logger;
    private readonly string _apiKey;

    public GeminiCoachingService(HttpClient httpClient, string apiKey, ILogger<GeminiCoachingService> logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _logger = logger;
    }

    public async Task<string> GetAdviceAsync(string patientContext, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return "Stay hydrated, get regular exercise, and maintain a balanced diet.";

        try
        {
            var systemPrompt = "You are a health coaching assistant. Provide personalized, " +
                "actionable wellness advice based on the patient's context. Keep advice practical and encouraging.";

            var request = new GeminiRequest
            {
                SystemInstruction = new GeminiContent
                {
                    Parts = [new GeminiPart { Text = systemPrompt }]
                },
                Contents =
                [
                    new GeminiContentEntry
                    {
                        Role = "user",
                        Parts = [new GeminiPart { Text = $"Please provide health coaching advice for this patient:\n{patientContext}" }]
                    }
                ],
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = 0.8f,
                    TopP = 0.9f,
                    TopK = 40,
                    MaxOutputTokens = 1024
                }
            };

            var url = $"?key={_apiKey}";
            using var content = new StringContent(
                JsonSerializer.Serialize(request, IntegrationJsonContext.Default.GeminiRequest),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[Gemini] Coaching request failed: {Status}", response.StatusCode);
                return "Stay hydrated, get regular exercise, and maintain a balanced diet.";
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var geminiResponse = await JsonSerializer.DeserializeAsync(stream, IntegrationJsonContext.Default.GeminiResponse, ct);

            var text = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            return text ?? "Stay hydrated, get regular exercise, and maintain a balanced diet.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Gemini] Coaching request exception");
            return "Stay hydrated, get regular exercise, and maintain a balanced diet.";
        }
    }
}

// Gemini API JSON models
public sealed record GeminiRequest
{
    [JsonPropertyName("system_instruction")]
    public GeminiContent? SystemInstruction { get; init; }

    [JsonPropertyName("contents")]
    public List<GeminiContentEntry> Contents { get; init; } = [];

    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig? GenerationConfig { get; init; }
}

public sealed record GeminiContent
{
    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; init; } = [];
}

public sealed record GeminiContentEntry
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; init; } = [];
}

public sealed record GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;
}

public sealed record GeminiGenerationConfig
{
    [JsonPropertyName("temperature")]
    public float Temperature { get; init; }

    [JsonPropertyName("topP")]
    public float TopP { get; init; }

    [JsonPropertyName("topK")]
    public int TopK { get; init; }

    [JsonPropertyName("maxOutputTokens")]
    public int MaxOutputTokens { get; init; }
}

public sealed record GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; init; }
}

public sealed record GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; init; }
}
