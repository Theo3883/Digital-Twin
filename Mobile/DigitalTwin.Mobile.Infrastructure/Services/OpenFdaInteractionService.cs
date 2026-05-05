using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using DigitalTwin.Mobile.Domain.Enums;
using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Infrastructure.Services;

public class OpenFdaInteractionService : IMedicationInteractionProvider
{
    private const int OpenFdaLimit = 20;

    private static readonly string[] HighRiskSignals =
    [
        "contraindicated",
        "life-threatening",
        "serious",
        "major interaction",
        "severe",
        "fatal",
        "hemorrhage",
        "bleeding risk"
    ];

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
        var requested = rxCuis
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requested.Count < 2)
            return [];

        try
        {
            var resolved = await ResolveDrugsAsync(requested, ct);
            var corpora = await BuildCorporaAsync(resolved, ct);
            return DetectPairwiseInteractions(resolved, corpora);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenFDA] Interaction check failed");
            return [];
        }
    }

    private async Task<List<ResolvedDrug>> ResolveDrugsAsync(List<string> requested, CancellationToken ct)
    {
        var resolved = new List<ResolvedDrug>(requested.Count);
        foreach (var rxCui in requested)
        {
            var ingredient = await ResolveToIngredientAsync(rxCui, ct);
            var name = await ResolveNameAsync(ingredient, ct);
            if (string.IsNullOrWhiteSpace(name) &&
                !ingredient.Equals(rxCui, StringComparison.OrdinalIgnoreCase))
            {
                name = await ResolveNameAsync(rxCui, ct);
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarning(
                    "[DDI] Name resolution failed for RxCUI {OriginalRxCui} (ingredient {IngredientRxCui}); skipping drug.",
                    rxCui, ingredient);
                continue;
            }

            _logger.LogInformation(
                "[DDI] Resolved RxCUI {OriginalRxCui} -> ingredient {IngredientRxCui} -> name '{DrugName}'.",
                rxCui, ingredient, name);

            resolved.Add(new ResolvedDrug(rxCui, ingredient, NormalizeDrugName(name)));
        }

        return resolved;
    }

    private async Task<Dictionary<string, string>> BuildCorporaAsync(List<ResolvedDrug> resolved, CancellationToken ct)
    {
        var corpora = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var drug in resolved)
        {
            var corpus = await FetchInteractionCorpusAsync(drug.NormalizedName, ct);
            if (corpus is null)
            {
                _logger.LogWarning(
                    "[DDI] openFDA lookup failed for '{DrugName}' (ingredient {IngredientRxCui}); skipping drug.",
                    drug.NormalizedName, drug.IngredientRxCui);
                continue;
            }

            _logger.LogInformation(
                "[DDI] openFDA corpus loaded for '{DrugName}' ({IngredientRxCui}), chars={Chars}.",
                drug.NormalizedName, drug.IngredientRxCui, corpus.Length);

            corpora[drug.IngredientRxCui] = corpus;
        }

        return corpora;
    }

    private List<MedicationInteraction> DetectPairwiseInteractions(
        List<ResolvedDrug> resolved,
        Dictionary<string, string> corpora)
    {
        var results = new List<MedicationInteraction>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < resolved.Count; i++)
        {
            for (var j = i + 1; j < resolved.Count; j++)
            {
                var a = resolved[i];
                var b = resolved[j];

                var pairKey = BuildPairKey(a.IngredientRxCui, b.IngredientRxCui);
                if (!seen.Add(pairKey))
                    continue;

                if (!corpora.TryGetValue(a.IngredientRxCui, out var corpusA) ||
                    !corpora.TryGetValue(b.IngredientRxCui, out var corpusB))
                    continue;

                try
                {
                    var aMentionsB = ContainsDrugMention(corpusA, b.NormalizedName);
                    var bMentionsA = ContainsDrugMention(corpusB, a.NormalizedName);
                    if (!aMentionsB && !bMentionsA)
                        continue;

                    var severity = EvaluateSeverity(corpusA, corpusB);
                    _logger.LogInformation(
                        "[DDI] Interaction detected: '{DrugA}' <-> '{DrugB}', severity={Severity}.",
                        a.NormalizedName, b.NormalizedName, severity);

                    results.Add(new MedicationInteraction
                    {
                        DrugARxCui = a.OriginalRxCui,
                        DrugBRxCui = b.OriginalRxCui,
                        Severity = severity,
                        Description = $"Label-based interaction signal detected between {a.NormalizedName} and {b.NormalizedName}."
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "[DDI] Pair evaluation failed for '{DrugA}' <-> '{DrugB}', continuing.",
                        a.NormalizedName,
                        b.NormalizedName);
                }
            }
        }

        return results;
    }

    private async Task<string> ResolveToIngredientAsync(string rxCui, CancellationToken ct)
    {
        try
        {
            var url = $"/REST/rxcui/{rxCui}/related.json?tty=IN";
            await using var stream = await _rxNavClient.GetStreamAsync(url, ct);
            var response = await JsonSerializer.DeserializeAsync(stream, IntegrationJsonContext.Default.RxNavRelatedResponse, ct);

            var ingredient = response?.RelatedGroup?.ConceptGroup
                ?.SelectMany(g => g.ConceptProperties ?? [])
                .Select(p => p.Rxcui)
                .FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));

            if (string.IsNullOrWhiteSpace(ingredient))
            {
                _logger.LogWarning("[DDI] No ingredient mapping for RxCUI {RxCui}; using original.", rxCui);
                return rxCui;
            }

            return ingredient;
        }
        catch
        {
            return rxCui;
        }
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

    private async Task<string?> FetchInteractionCorpusAsync(string normalizedDrugName, CancellationToken ct)
    {
        try
        {
            var encodedName = Uri.EscapeDataString($"\"{ToAsciiUpper(normalizedDrugName)}\"");
            var url = $"?search=openfda.substance_name:{encodedName}&limit={OpenFdaLimit}";
            await using var stream = await _openFdaClient.GetStreamAsync(url, ct);
            var response = await JsonSerializer.DeserializeAsync(stream, IntegrationJsonContext.Default.OpenFdaResponse, ct);

            if (response?.Results == null)
                return string.Empty;

            var builder = new StringBuilder(capacity: 16_384);
            foreach (var result in response.Results)
            {
                AppendSectionArray(result.DrugInteractions, builder);
                AppendSectionArray(result.WarningsAndCautions, builder);
                AppendSectionArray(result.Contraindications, builder);
                AppendSectionArray(result.BoxedWarning, builder);
            }

            return builder.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static void AppendSectionArray(List<string>? values, StringBuilder builder)
    {
        if (values == null)
            return;

        foreach (var value in values)
        {
            AppendNormalizedAscii(value, builder);
        }
    }

    private static bool ContainsDrugMention(string corpus, string drugName)
    {
        if (string.IsNullOrWhiteSpace(corpus) || string.IsNullOrWhiteSpace(drugName))
            return false;

        var normalizedDrug = NormalizeDrugName(drugName);
        if (string.IsNullOrWhiteSpace(normalizedDrug))
            return false;

        return ContainsWholeTerm(corpus, normalizedDrug);
    }

    private static InteractionSeverity EvaluateSeverity(string corpusA, string corpusB)
    {
        return HighRiskSignals.Any(s => corpusA.Contains(s, StringComparison.Ordinal)
                                     || corpusB.Contains(s, StringComparison.Ordinal))
            ? InteractionSeverity.High
            : InteractionSeverity.Medium;
    }

    private static string NormalizeDrugName(string name)
    {
        var builder = new StringBuilder(name.Length + 2);
        AppendNormalizedAscii(name, builder);
        return builder.ToString().Trim();
    }

    private static bool ContainsWholeTerm(string corpus, string term)
    {
        if (corpus.Length < term.Length)
            return false;

        var index = 0;
        while (index <= corpus.Length - term.Length)
        {
            index = corpus.IndexOf(term, index, StringComparison.Ordinal);
            if (index < 0)
                return false;

            var leftBoundary = index == 0 || corpus[index - 1] == ' ';
            var rightIndex = index + term.Length;
            var rightBoundary = rightIndex == corpus.Length || corpus[rightIndex] == ' ';

            if (leftBoundary && rightBoundary)
                return true;

            index++;
        }

        return false;
    }

    private static void AppendNormalizedAscii(string? value, StringBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var previousIsSpace = builder.Length == 0 || builder[^1] == ' ';

        foreach (var c in value)
        {
            if (c >= 'A' && c <= 'Z')
            {
                builder.Append((char)(c + 32));
                previousIsSpace = false;
            }
            else if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
            {
                builder.Append(c);
                previousIsSpace = false;
            }
            else if (!previousIsSpace)
            {
                builder.Append(' ');
                previousIsSpace = true;
            }
        }

        if (builder.Length > 0 && builder[^1] != ' ')
            builder.Append(' ');
    }

    private static string ToAsciiUpper(string value)
    {
        var buffer = value.ToCharArray();
        for (var i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] >= 'a' && buffer[i] <= 'z')
                buffer[i] = (char)(buffer[i] - 32);
        }

        return new string(buffer);
    }

    private static string BuildPairKey(string a, string b) =>
        string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0 ? $"{a}|{b}" : $"{b}|{a}";

    private sealed record ResolvedDrug(string OriginalRxCui, string IngredientRxCui, string NormalizedName);
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

// RxNav related response (ingredient normalization)
public sealed record RxNavRelatedResponse
{
    [JsonPropertyName("relatedGroup")]
    public RxNavRelatedGroup? RelatedGroup { get; init; }
}

public sealed record RxNavRelatedGroup
{
    [JsonPropertyName("conceptGroup")]
    public List<RxNavConceptGroup>? ConceptGroup { get; init; }
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

    [JsonPropertyName("boxed_warning")]
    public List<string>? BoxedWarning { get; init; }
}
