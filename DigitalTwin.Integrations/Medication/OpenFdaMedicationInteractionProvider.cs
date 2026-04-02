using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Models;
using DigitalTwin.Integrations.Medication.DTOs;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Integrations.Medication;

/// <summary>
/// Medication interaction provider built from scratch on top of currently active APIs:
/// - RxNav (name + ingredient normalization only)
/// - openFDA Drug Label API (interaction/warning text sections)
///
/// Why this exists:
/// RxNav's dedicated interaction APIs were discontinued. This adapter extracts
/// interaction evidence from label text and maps it to domain interactions.
/// </summary>
public sealed class OpenFdaMedicationInteractionProvider : IMedicationInteractionProvider
{
    private const string RxNavBaseUrl = "https://rxnav.nlm.nih.gov/REST";
    private const string OpenFdaBaseUrl = "https://api.fda.gov/drug/label.json";
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

    private readonly HttpClient _http;
    private readonly ILogger<OpenFdaMedicationInteractionProvider> _logger;

    public OpenFdaMedicationInteractionProvider(
        HttpClient http,
        ILogger<OpenFdaMedicationInteractionProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IEnumerable<MedicationInteraction>> GetInteractionsAsync(
        IEnumerable<string> rxCuis,
        CancellationToken ct = default)
    {
        var requested = rxCuis
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requested.Count < 2)
            return [];

        // Resolve RxCUI -> ingredient RxCUI -> canonical drug name.
        var resolved = new List<ResolvedDrug>(requested.Count);
        foreach (var rxcui in requested)
        {
            var ingredient = await ResolveToIngredientAsync(rxcui, ct);
            var name = await ResolveNameAsync(ingredient, ct);
            if (string.IsNullOrWhiteSpace(name) &&
                !ingredient.Equals(rxcui, StringComparison.OrdinalIgnoreCase))
            {
                // Fallback: if ingredient CUI has no properties, try original CUI.
                name = await ResolveNameAsync(rxcui, ct);
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarning(
                    "[DDI] Name resolution failed for RxCUI {OriginalRxCui} (ingredient {IngredientRxCui}); skipping drug from interaction check.",
                    rxcui, ingredient);
                continue;
            }

            _logger.LogInformation(
                "[DDI] Resolved RxCUI {OriginalRxCui} -> ingredient {IngredientRxCui} -> name '{DrugName}'.",
                rxcui, ingredient, name);

            resolved.Add(new ResolvedDrug(rxcui, ingredient, NormalizeDrugName(name)));
        }

        // Build a text corpus per drug from openFDA sections.
        var corpora = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var drug in resolved)
        {
            var corpus = await FetchInteractionCorpusAsync(drug.NormalizedName, ct);
            if (corpus is null)
            {
                _logger.LogWarning(
                    "[DDI] openFDA lookup failed for '{DrugName}' (ingredient {IngredientRxCui}); skipping drug from interaction check.",
                    drug.NormalizedName, drug.IngredientRxCui);
                continue;
            }

            _logger.LogInformation(
                "[DDI] openFDA corpus loaded for '{DrugName}' ({IngredientRxCui}), chars={Chars}.",
                drug.NormalizedName, drug.IngredientRxCui, corpus.Length);
            corpora[drug.IngredientRxCui] = corpus;
        }

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

                var aMentionsB = ContainsDrugMention(corpusA, b.NormalizedName);
                var bMentionsA = ContainsDrugMention(corpusB, a.NormalizedName);
                if (!aMentionsB && !bMentionsA)
                {
                    _logger.LogDebug(
                        "[DDI] No cross-mention found between '{DrugA}' and '{DrugB}'.",
                        a.NormalizedName, b.NormalizedName);
                    continue;
                }

                var severity = EvaluateSeverity(corpusA, corpusB);
                _logger.LogInformation(
                    "[DDI] Interaction detected: '{DrugA}' <-> '{DrugB}', severity={Severity}.",
                    a.NormalizedName, b.NormalizedName, severity);
                results.Add(new MedicationInteraction
                {
                    DrugARxCui = a.IngredientRxCui,
                    DrugBRxCui = b.IngredientRxCui,
                    Severity = severity,
                    Description = $"Label-based interaction signal detected between {a.NormalizedName} and {b.NormalizedName}."
                });
            }
        }

        return results;
    }

    private async Task<string> ResolveToIngredientAsync(string rxCui, CancellationToken ct)
    {
        try
        {
            var url = $"{RxNavBaseUrl}/rxcui/{rxCui}/related.json?tty=IN";
            var resp = await _http.GetFromJsonAsync<RxNavRelatedResponse>(url, ct);
            var ingredient = resp?.RelatedGroup?.ConceptGroup
                ?.SelectMany(g => g.ConceptProperties ?? [])
                .Select(p => p.Rxcui)
                .FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));

            if (string.IsNullOrWhiteSpace(ingredient))
            {
                _logger.LogWarning(
                    "[DDI] No ingredient mapping for RxCUI {RxCui}; using original value.",
                    rxCui);
                return rxCui;
            }

            return ingredient;
        }
        catch
        {
            _logger.LogWarning("[DDI] Ingredient mapping failed for RxCUI {RxCui}; using original value.", rxCui);
            return rxCui;
        }
    }

    private async Task<string?> ResolveNameAsync(string rxCui, CancellationToken ct)
    {
        try
        {
            var url = $"{RxNavBaseUrl}/rxcui/{rxCui}/properties.json";
            using var stream = await _http.GetStreamAsync(url, ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("properties", out var properties) &&
                properties.TryGetProperty("name", out var nameProp))
            {
                return nameProp.GetString();
            }

            return null;
        }
        catch
        {
            _logger.LogWarning("[DDI] Properties lookup failed for RxCUI {RxCui}.", rxCui);
            return null;
        }
    }

    private async Task<string?> FetchInteractionCorpusAsync(string normalizedDrugName, CancellationToken ct)
    {
        try
        {
            var substance = Uri.EscapeDataString($"\"{normalizedDrugName.ToUpperInvariant()}\"");
            var query = $"search=openfda.substance_name:{substance}&limit={OpenFdaLimit}";
            var url = $"{OpenFdaBaseUrl}?{query}";

            using var stream = await _http.GetStreamAsync(url, ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("results", out var results))
                return string.Empty;

            var parts = new List<string>();
            foreach (var item in results.EnumerateArray())
            {
                AppendSectionArray(item, "drug_interactions", parts);
                AppendSectionArray(item, "warnings_and_cautions", parts);
                AppendSectionArray(item, "contraindications", parts);
                AppendSectionArray(item, "boxed_warning", parts);
            }

            return string.Join(" ", parts).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private static void AppendSectionArray(JsonElement item, string key, List<string> parts)
    {
        if (!item.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return;

        foreach (var entry in arr.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                var value = entry.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    parts.Add(value);
            }
        }
    }

    private static bool ContainsDrugMention(string corpus, string drugName)
    {
        if (string.IsNullOrWhiteSpace(corpus) || string.IsNullOrWhiteSpace(drugName))
            return false;

        var pattern = $@"\b{Regex.Escape(drugName.ToLowerInvariant())}\b";
        return Regex.IsMatch(corpus, pattern, RegexOptions.CultureInvariant);
    }

    private static InteractionSeverity EvaluateSeverity(string corpusA, string corpusB)
    {
        var combined = $"{corpusA} {corpusB}";
        return HighRiskSignals.Any(s => combined.Contains(s, StringComparison.OrdinalIgnoreCase))
            ? InteractionSeverity.High
            : InteractionSeverity.Medium;
    }

    private static string NormalizeDrugName(string name)
    {
        var n = name.Trim().ToLowerInvariant();
        n = Regex.Replace(n, @"\s+", " ");
        return n;
    }

    private static string BuildPairKey(string a, string b) =>
        string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0 ? $"{a}|{b}" : $"{b}|{a}";

    private sealed record ResolvedDrug(string OriginalRxCui, string IngredientRxCui, string NormalizedName);
}
