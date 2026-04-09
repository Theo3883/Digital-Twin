using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Infrastructure.Services;

public class OpenFdaInteractionService : IMedicationInteractionProvider
{
    private readonly HttpClient _rxNavClient;
    private readonly HttpClient _openFdaClient;
    private readonly ILogger<OpenFdaInteractionService> _logger;

    public OpenFdaInteractionService(
        IHttpClientFactory httpClientFactory,
        ILogger<OpenFdaInteractionService> logger)
    {
        _rxNavClient = httpClientFactory.CreateClient("RxNav");
        _openFdaClient = httpClientFactory.CreateClient("OpenFda");
        _logger = logger;
    }

    public async Task<IEnumerable<MedicationInteraction>> GetInteractionsAsync(
        IEnumerable<string> rxCuis, CancellationToken ct = default)
    {
        var rxCuiList = rxCuis.ToList();
        if (rxCuiList.Count < 2) return [];

        var interactions = new List<MedicationInteraction>();

        try
        {
            // Resolve drug names from RxCUIs
            var drugNames = new Dictionary<string, string>();
            foreach (var rxCui in rxCuiList)
            {
                var name = await ResolveNameAsync(rxCui, ct);
                if (name != null)
                    drugNames[rxCui] = name;
            }

            // Check pairwise interactions via openFDA
            for (int i = 0; i < rxCuiList.Count; i++)
            {
                for (int j = i + 1; j < rxCuiList.Count; j++)
                {
                    var a = rxCuiList[i];
                    var b = rxCuiList[j];

                    if (!drugNames.TryGetValue(a, out var nameA) || !drugNames.TryGetValue(b, out var nameB))
                        continue;

                    var interaction = await CheckPairAsync(a, b, nameA, nameB, ct);
                    if (interaction != null)
                        interactions.Add(interaction);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenFDA] Interaction check failed");
        }

        return interactions;
    }

    private async Task<string?> ResolveNameAsync(string rxCui, CancellationToken ct)
    {
        try
        {
            var url = $"/REST/rxcui/{rxCui}/properties.json";
            await using var stream = await _rxNavClient.GetStreamAsync(url, ct);
            var response = await JsonSerializer.DeserializeAsync(stream, IntegrationJsonContext.Default.RxNavPropertiesResponse, ct);
            return response?.Properties?.Name;
        }
        catch
        {
            return null;
        }
    }

    private async Task<MedicationInteraction?> CheckPairAsync(
        string rxCuiA, string rxCuiB, string nameA, string nameB, CancellationToken ct)
    {
        try
        {
            var encodedName = Uri.EscapeDataString($"\"{nameA}\"");
            var url = $"?search=openfda.substance_name:{encodedName}&limit=5";
            await using var stream = await _openFdaClient.GetStreamAsync(url, ct);
            var response = await JsonSerializer.DeserializeAsync(stream, IntegrationJsonContext.Default.OpenFdaResponse, ct);

            if (response?.Results == null) return null;

            foreach (var result in response.Results)
            {
                var allText = string.Join(" ",
                    result.DrugInteractions ?? [],
                    result.WarningsAndCautions ?? [],
                    result.Contraindications ?? []);

                if (allText.Contains(nameB, StringComparison.OrdinalIgnoreCase))
                {
                    var severity = DetermineSeverity(allText);
                    return new MedicationInteraction
                    {
                        DrugARxCui = rxCuiA,
                        DrugBRxCui = rxCuiB,
                        Severity = severity,
                        Description = $"Potential interaction between {nameA} and {nameB}."
                    };
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static InteractionSeverity DetermineSeverity(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("contraindicated") || lower.Contains("fatal") || lower.Contains("do not use"))
            return InteractionSeverity.High;
        if (lower.Contains("serious") || lower.Contains("major") || lower.Contains("avoid"))
            return InteractionSeverity.Medium;
        return InteractionSeverity.Low;
    }
}

// RxNav properties response
public sealed record RxNavPropertiesResponse
{
    [JsonPropertyName("properties")]
    public RxNavProperties? Properties { get; init; }
}

public sealed record RxNavProperties
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

// openFDA response
public sealed record OpenFdaResponse
{
    [JsonPropertyName("results")]
    public List<OpenFdaResult>? Results { get; init; }
}

public sealed record OpenFdaResult
{
    [JsonPropertyName("drug_interactions")]
    public List<string>? DrugInteractions { get; init; }

    [JsonPropertyName("warnings_and_cautions")]
    public List<string>? WarningsAndCautions { get; init; }

    [JsonPropertyName("contraindications")]
    public List<string>? Contraindications { get; init; }
}
